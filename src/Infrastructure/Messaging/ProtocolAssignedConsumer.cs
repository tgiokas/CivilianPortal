using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Confluent.Kafka;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Infrastructure.Messaging;

/// Kafka consumer that listens for protocol assignment events from DMS.
/// When DMS finishes processing a citizen application, it publishes to
/// the protocol topic. This consumer picks it up and updates the CitizenPortal DB.
public class ProtocolAssignedConsumer : BackgroundService
{
    private const int MaxProcessingAttempts = 5;
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(1);

    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProtocolAssignedConsumer> _logger;
    private readonly string _topic;

    // Tracks consecutive failures for the offset currently being retried so a
    // persistently failing message is skipped instead of blocking the partition.
    private TopicPartitionOffset? _retryOffset;
    private int _retryAttempts;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProtocolAssignedConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<ProtocolAssignedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var settings = kafkaOptions.Value;
        _topic = settings.ProtocolTopic;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            ReconnectBackoffMs = settings.ReconnectBackoffMs,
            ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,
            SocketConnectionSetupTimeoutMs = settings.SocketConnectionSetupTimeoutMs,
            SocketTimeoutMs = settings.SocketTimeoutMs,

            GroupId = settings.GroupId,
            AutoOffsetReset = settings.AutoOffsetReset,
            EnableAutoCommit = false,
            SessionTimeoutMs = settings.SessionTimeoutMs,
            MaxPollIntervalMs = settings.MaxPollIntervalMs,
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to let the rest of the app start
        await Task.Yield();

        _logger.LogInformation("ProtocolAssignedConsumer started. Subscribing to {Topic}", _topic);

        // Retry subscribe until Kafka is ready
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _consumer.Subscribe(_topic);
                _logger.LogInformation("Successfully subscribed to topics.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka not ready, retrying in 5s...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = _consumer.Consume(TimeSpan.FromSeconds(5));
                    if (result == null) continue;

                    _logger.LogInformation("Message from topic {Topic} consumed", result.Topic);

                    var payload = ParsePayload(result.Message.Value);

                    if (payload is not null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                        var updateResult = await appService.UpdateStatusFromDmsAsync(payload);

                        if (!updateResult.Success)
                        {
                            if (updateResult.ErrorCode == ErrorCodes.PORTAL.ApplicationAlreadyProtocoled)
                            {
                                // Normal idempotency outcome (duplicate delivery from Kafka or a
                                // DMS-side retry). Commit and move on.
                                _logger.LogInformation(
                                    "Protocol assignment for {PublicId} already applied (ErrorCode={ErrorCode}). Committing offset.",
                                    payload.ApplicationPublicId, updateResult.ErrorCode);
                            }
                            else if (updateResult.ErrorCode == ErrorCodes.PORTAL.ApplicationNotFound)
                            {
                                // Application unknown. Retrying will not help — log and commit.
                                _logger.LogWarning(
                                    "Protocol assignment for {PublicId} skipped (ErrorCode={ErrorCode}): {Message}. Committing offset.",
                                    payload.ApplicationPublicId, updateResult.ErrorCode, updateResult.Message);
                            }
                            else
                            {
                                // Transient failure — throw so the outer catch seeks back and retries.
                                throw new InvalidOperationException(
                                    $"UpdateStatusFromDmsAsync failed for {payload.ApplicationPublicId}: " +
                                    $"{updateResult.ErrorCode} – {updateResult.Message}");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Received unparseable message at {TPO}; committing to skip.",
                            result.TopicPartitionOffset);
                    }

                    CommitAndReset(result);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "ConsumeException at {TPO}: {Reason}",
                        result?.TopicPartitionOffset, ex.Error.Reason);
                    await Task.Delay(RetryBackoff, stoppingToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "JSON error at {TPO}; committing to skip poison message.",
                        result?.TopicPartitionOffset);
                    if (result is not null) CommitAndReset(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // normal shutdown
                }
                catch (Exception ex)
                {
                    if (result is null)
                    {
                        _logger.LogError(ex, "Unhandled processing error. Backing off briefly.");
                        await Task.Delay(RetryBackoff, stoppingToken);
                    }
                    else
                    {
                        await HandleRetryableFailureAsync(result, ex, stoppingToken);
                    }
                }
            }
        }
        finally
        {
            try { _consumer.Close(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
        }
    }

    /// Rewinds to the failed offset so the next poll re-delivers it, then backs off.
    /// After MaxProcessingAttempts consecutive failures the message is committed as
    /// poison so the partition can move on.
    private async Task HandleRetryableFailureAsync(
        ConsumeResult<string, string> result, Exception ex, CancellationToken ct)
    {
        if (_retryOffset == result.TopicPartitionOffset)
            _retryAttempts++;
        else
        {
            _retryOffset = result.TopicPartitionOffset;
            _retryAttempts = 1;
        }

        if (_retryAttempts >= MaxProcessingAttempts)
        {
            _logger.LogCritical(ex,
                "POISON — committing to unblock partition. " +
                "TPO={TPO}, attempts={Attempts}, payload={Payload}",
                result.TopicPartitionOffset, _retryAttempts, result.Message?.Value);
            CommitAndReset(result);
            return;
        }

        _logger.LogWarning(ex,
            "Retryable failure at {TPO} (attempt {Attempt}/{Max}). Seeking back and backing off.",
            result.TopicPartitionOffset, _retryAttempts, MaxProcessingAttempts);

        try { _consumer.Seek(result.TopicPartitionOffset); }
        catch (Exception seekEx) { _logger.LogWarning(seekEx, "Seek failed."); }

        await Task.Delay(RetryBackoff, ct);
    }

    private void CommitAndReset(ConsumeResult<string, string> result)
    {
        _consumer.Commit(result);
        _retryOffset = null;
        _retryAttempts = 0;
        _logger.LogInformation("Offset committed at {TPO}", result.TopicPartitionOffset);
    }

    private static ProtocolAssignedEvent? ParsePayload(string payload)
    {
        try
        {
            var env = JsonSerializer.Deserialize<KafkaMessage<ProtocolAssignedEvent>>(payload, JsonOpts);
            if (env?.Content is not null) return env.Content;
        }
        catch (JsonException) { }

        try
        {
            var envRaw = JsonSerializer.Deserialize<KafkaMessage<string>>(payload, JsonOpts);
            if (!string.IsNullOrWhiteSpace(envRaw?.Content))
                return JsonSerializer.Deserialize<ProtocolAssignedEvent>(envRaw.Content, JsonOpts);
        }
        catch (JsonException) { }

        try
        {
            return JsonSerializer.Deserialize<ProtocolAssignedEvent>(payload, JsonOpts);
        }
        catch (JsonException) { }

        return null;
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
