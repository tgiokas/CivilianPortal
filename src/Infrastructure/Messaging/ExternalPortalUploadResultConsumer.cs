using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Confluent.Kafka;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Dtos;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Infrastructure.Messaging;

/// Kafka consumer that listens for upload/archive results from ARCHIUM (spec section 3,
/// Φάση 1). When ARCHIUM finishes processing an UploadRequestedEvent, it publishes to
/// the upload.result topic. This consumer picks it up and updates the matching UploadJob.
public class ExternalPortalUploadResultConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExternalPortalUploadResultConsumer> _logger;
    private readonly string _topic;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ExternalPortalUploadResultConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<ExternalPortalUploadResultConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var settings = kafkaOptions.Value;
        _topic = settings.UploadResultTopic;

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            ReconnectBackoffMs = settings.ReconnectBackoffMs,
            ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,
            SocketConnectionSetupTimeoutMs = settings.SocketConnectionSetupTimeoutMs,
            SocketTimeoutMs = settings.SocketTimeoutMs,

            // Distinct group so this consumer's offsets don't collide with ProtocolAssignedConsumer's.
            GroupId = $"{settings.GroupId}-external-portal",
            AutoOffsetReset = settings.AutoOffsetReset,
            EnableAutoCommit = false,
            SessionTimeoutMs = settings.SessionTimeoutMs,
            MaxPollIntervalMs = settings.MaxPollIntervalMs,
        };
        consumerConfig.ApplySasl(settings);

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _logger.LogInformation("ExternalPortalUploadResultConsumer started. Subscribing to {Topic}", _topic);

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
                        var uploadJobRepo = scope.ServiceProvider.GetRequiredService<IUploadJobRepository>();

                        var success = payload.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);

                        var updated = success
                            ? await uploadJobRepo.MarkCompletedAsync(
                                payload.JobId, payload.ArchiumFileId ?? 0, payload.ProtocolNumber, payload.ProtocolYear)
                            : await uploadJobRepo.MarkFailedAsync(
                                payload.JobId, payload.ErrorCode ?? "UNKNOWN", payload.ErrorMessage ?? "Upload failed.");

                        if (!updated)
                        {
                            _logger.LogWarning(
                                "Upload job {JobId} not updated (unknown job or already completed). Committing offset.",
                                payload.JobId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Received unparseable message at {TPO}; committing to skip.",
                            result.TopicPartitionOffset);
                    }

                    _consumer.Commit(result);

                    _logger.LogInformation("Offset committed at {TPO}", result.TopicPartitionOffset);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "ConsumeException at {TPO}: {Reason}",
                        result?.TopicPartitionOffset, ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "JSON error at {TPO}; committing to skip poison message.",
                        result?.TopicPartitionOffset);
                    if (result is not null) _consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled processing error at {TPO}. Backing off briefly.",
                        result?.TopicPartitionOffset);
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            try { _consumer.Close(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
        }
    }

    private static UploadResultEvent? ParsePayload(string payload)
    {
        try
        {
            var env = JsonSerializer.Deserialize<KafkaMessage<UploadResultEvent>>(payload, JsonOpts);
            if (env?.Content is not null) return env.Content;
        }
        catch (JsonException) { /* fall through */ }

        try
        {
            var envRaw = JsonSerializer.Deserialize<KafkaMessage<string>>(payload, JsonOpts);
            if (!string.IsNullOrWhiteSpace(envRaw?.Content))
                return JsonSerializer.Deserialize<UploadResultEvent>(envRaw.Content, JsonOpts);
        }
        catch (JsonException) { /* fall through */ }

        try
        {
            return JsonSerializer.Deserialize<UploadResultEvent>(payload, JsonOpts);
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
