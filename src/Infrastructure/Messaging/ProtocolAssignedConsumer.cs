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
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProtocolAssignedConsumer> _logger;
    private readonly string _topic;

    private const int MaxDeliveryAttempts = 5;
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(1);

    // Tracks consecutive retry attempts for a single offset so a persistently
    // failing message is skipped instead of blocking the partition forever.
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

                    if (string.IsNullOrWhiteSpace(result.Message.Value))
                    {
                        // Tombstone / empty value — nothing to process, skip it.
                        _logger.LogWarning("Received empty message at {TPO}; committing to skip.",
                            result.TopicPartitionOffset);
                        CommitAndReset(result);
                        continue;
                    }

                    var payload = ParsePayload(result.Message.Value);

                    if (payload is null)
                    {
                        _logger.LogWarning(
                            "Received unparseable message at {TPO}; committing to skip.",
                            result.TopicPartitionOffset);
                        CommitAndReset(result);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                    var updateResult = await appService.UpdateStatusFromDmsAsync(payload);

                    if (!updateResult.Success && updateResult.ErrorCode != ErrorCodes.PORTAL.ApplicationNotFound)
                    {
                        // Transient or unexpected failure — re-read the same message and retry.
                        await HandleRetryableFailureAsync(
                            result,
                            $"UpdateStatusFromDmsAsync failed for {payload.ApplicationPublicId}: " +
                            $"{updateResult.ErrorCode} – {updateResult.Message}",
                            stoppingToken);
                        continue;
                    }

                    if (!updateResult.Success)
                    {
                        // Application unknown or protocol already assigned (idempotency guard).
                        // Retrying will not help — log and commit.
                        _logger.LogWarning(
                            "Protocol assignment for {PublicId} skipped (ErrorCode={ErrorCode}): {Message}. Committing offset.",
                            payload.ApplicationPublicId, updateResult.ErrorCode, updateResult.Message);
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
                        _logger.LogError(ex, "Unhandled processing error with no message. Backing off briefly.");
                        await Task.Delay(RetryBackoff, stoppingToken);
                    }
                    else
                    {
                        await HandleRetryableFailureAsync(result, ex.Message, stoppingToken);
                    }
                }
            }
        }
        finally
        {
            try { _consumer.Close(); } // leave group & commit last offsets
            catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
        }

    }

    /// Re-reads the same message on the next poll (via Seek) and backs off, so a
    /// transient failure is actually retried in order. After MaxDeliveryAttempts
    /// the message is skipped (committed) with an error log to avoid blocking the
    /// partition on a persistently failing message.
    private async Task HandleRetryableFailureAsync(
        ConsumeResult<string, string> result, string reason, CancellationToken ct)
    {
        if (_retryOffset is not null && _retryOffset == result.TopicPartitionOffset)
            _retryAttempts++;
        else
        {
            _retryOffset = result.TopicPartitionOffset;
            _retryAttempts = 1;
        }

        if (_retryAttempts >= MaxDeliveryAttempts)
        {
            _logger.LogError(
                "Giving up on {TPO} after {Attempts} attempts: {Reason}. Committing to skip.",
                result.TopicPartitionOffset, _retryAttempts, reason);
            CommitAndReset(result);
            return;
        }

        _logger.LogWarning(
            "Retryable failure at {TPO} (attempt {Attempt}/{Max}): {Reason}. Seeking back and backing off.",
            result.TopicPartitionOffset, _retryAttempts, MaxDeliveryAttempts, reason);

        try { _consumer.Seek(result.TopicPartitionOffset); }
        catch (Exception ex) { _logger.LogWarning(ex, "Seek failed; will resume from last committed offset."); }

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
        // 1) Envelope with typed Content
        try
        {
            var env = JsonSerializer.Deserialize<KafkaMessage<ProtocolAssignedEvent>>(payload, JsonOpts);
            if (env?.Content is not null) return env.Content;
        }
        catch (JsonException) { /* fall through */ }

        // 2) Envelope with string Content
        try
        {
            var envRaw = JsonSerializer.Deserialize<KafkaMessage<string>>(payload, JsonOpts);
            if (!string.IsNullOrWhiteSpace(envRaw?.Content))
                return JsonSerializer.Deserialize<ProtocolAssignedEvent>(envRaw.Content, JsonOpts);
        }
        catch (JsonException) { /* fall through */ }

        // 3) Bare DTO
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