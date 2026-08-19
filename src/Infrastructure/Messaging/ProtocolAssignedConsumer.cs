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
public sealed class ProtocolAssignedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProtocolAssignedConsumer> _logger;
    private readonly ConsumerConfig _consumerConfig;
    private readonly string _topic;
    private volatile bool _fatalError;

    private const int RebuildDelayMs = 5000;
    private const int TransientBackoffMs = 1000;

    // Retry policy for transient update failures (e.g. DB blip).
    // 5 attempts with exponential backoff (1+2+4+8s ≈ 15s total) stays well under
    // MaxPollIntervalMs, so Kafka won't consider the consumer dead mid-retry.
    private const int MaxDeliveryAttempts = 5;
    private const int DeliveryRetryBaseMs = 1000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ProtocolAssignedConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<ProtocolAssignedConsumer> logger)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        var settings = kafkaOptions.Value;
        _topic = settings.ProtocolTopic;

        // Config is built once and reused for every (re)built consumer instance.
        _consumerConfig = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            ReconnectBackoffMs = settings.ReconnectBackoffMs,
            ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,
            SocketConnectionSetupTimeoutMs = settings.SocketConnectionSetupTimeoutMs,
            SocketTimeoutMs = settings.SocketTimeoutMs,
            SocketKeepaliveEnable = true,

            GroupId = settings.GroupId,
            AutoOffsetReset = settings.AutoOffsetReset,
            EnableAutoCommit = false,
            SessionTimeoutMs = settings.SessionTimeoutMs,
            MaxPollIntervalMs = settings.MaxPollIntervalMs,
        };

        _consumerConfig.ApplySasl(settings);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to let the rest of the app start
        await Task.Yield();

        _logger.LogInformation("ProtocolAssignedConsumer starting. Topic: {Topic}", _topic);

        // Outer lifecycle loop: each iteration owns one consumer instance.
        // If that instance dies (fatal error / unexpected crash) we dispose it and build a fresh one.
        while (!stoppingToken.IsCancellationRequested)
        {
            IConsumer<string, string>? consumer = null;

            try
            {
                consumer = BuildConsumer();

                // Subscribe to topics
                consumer.Subscribe(_topic);
                _logger.LogInformation("Subscribed to topic {Topic}.", _topic);

                await ConsumeLoop(consumer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer instance crashed. Rebuilding in {Delay}ms...", RebuildDelayMs);
            }
            finally
            {
                if (consumer is not null)
                {
                    try { consumer.Close(); }      // leave group & commit last offsets
                    catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
                    consumer.Dispose();
                }
            }

            // Pause before rebuilding so we don't hot-loop while the broker is still down.
            if (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(RebuildDelayMs, stoppingToken); }
                catch (OperationCanceledException) { /* shutting down */ }
            }
        }
    }

    private IConsumer<string, string> BuildConsumer()
    {
        _fatalError = false;

        return new ConsumerBuilder<string, string>(_consumerConfig)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                {
                    // A fatal error means THIS consumer instance is dead and can
                    // only be recovered by disposing it and creating a new one.
                    _logger.LogError("Fatal Kafka error: {Reason}. Consumer will be rebuilt.", e.Reason);
                    _fatalError = true;
                }
                else
                {
                    // Transient (e.g. "all brokers down" during an outage) – librdkafka
                    // will keep reconnecting on its own using the backoff settings.
                    _logger.LogWarning("Kafka error: {Reason}", e.Reason);
                }
            })
            .SetLogHandler((_, msg) =>
            {
                _logger.LogDebug("Kafka[{Name}] {Level}: {Message}", msg.Name, msg.Level, msg.Message);
            })
            .Build();
    }

    private async Task ConsumeLoop(IConsumer<string, string> consumer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // The error handler may flag a fatal state on a background thread.
            // Break out so the outer loop can rebuild the consumer.
            if (_fatalError)
            {
                _logger.LogWarning("Fatal error flagged; tearing down consumer to rebuild.");
                return;
            }

            ConsumeResult<string, string>? result = null;

            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(5));
                if (result == null) continue; // timeout, no message (e.g. broker down) -> keep polling

                _logger.LogInformation("Message from topic {Topic} consumed", result.Topic);

                var payload = ParsePayload(result.Message.Value);
                if (payload is null)
                {
                    _logger.LogWarning("Skipping message at {TPO}: unable to parse payload.",
                        result.TopicPartitionOffset);
                    consumer.Commit(result); // commit to avoid reprocessing a poison message
                    continue;
                }

                // Retry transient delivery failures in place, BEFORE committing, so the
                // offset only advances once the message is truly handled.
                await DeliverWithRetriesAsync(payload, result.TopicPartitionOffset, stoppingToken);

                consumer.Commit(result); // success
                _logger.LogInformation("Offset committed at {TPO}", result.TopicPartitionOffset);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // normal shutdown
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "ConsumeException at {TPO}: {Reason}",
                    result?.TopicPartitionOffset, ex.Error.Reason);

                if (ex.Error.IsFatal)
                {
                    _logger.LogError("ConsumeException is fatal; rebuilding consumer.");
                    return; // outer loop rebuilds
                }

                await Task.Delay(TransientBackoffMs, stoppingToken);
            }
            catch (KafkaException ex)
            {
                // e.g. Commit() failing while the broker is unreachable.
                _logger.LogError(ex, "KafkaException at {TPO}: {Reason}",
                    result?.TopicPartitionOffset, ex.Error.Reason);

                if (ex.Error.IsFatal)
                    return;

                await Task.Delay(TransientBackoffMs, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled processing error at {TPO}. Backing off briefly.",
                    result?.TopicPartitionOffset);
                await Task.Delay(TransientBackoffMs, stoppingToken);
            }
        }
    }

    /// Applies a protocol assignment, retrying transient failures (e.g. a brief DB outage) with exponential backoff.
    /// Terminal outcomes (unknown application, already protocoled) are logged and treated as done so the offset can be committed.
    /// After the retry budget is exhausted the message is dropped with a Critical log and this returns,
    /// allowing the caller to commit so the partition is not blocked indefinitely.
    private async Task DeliverWithRetriesAsync(
        ProtocolAssignedEvent payload, TopicPartitionOffset tpo, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                // Fresh scope per attempt: a new IApplicationService and freshly-resolved dependencies.
                using var scope = _scopeFactory.CreateScope();
                var appService = scope.ServiceProvider.GetRequiredService<IApplicationService>();

                var updateResult = await appService.UpdateStatusFromDmsAsync(payload);

                if (updateResult.Success)
                    return;

                if (IsTerminal(updateResult.ErrorCode))
                {
                    _logger.LogWarning(
                        "Protocol assignment for {PublicId} at {TPO} skipped (ErrorCode={ErrorCode}): {Message}. Committing offset.",
                        payload.ApplicationPublicId, tpo, updateResult.ErrorCode, updateResult.Message);
                    return;
                }

                if (attempt >= MaxDeliveryAttempts)
                {
                    _logger.LogCritical(
                        "Dropping message at {TPO} for {PublicId} after {Attempts} transient failures (ErrorCode={ErrorCode}): {Message}.",
                        tpo, payload.ApplicationPublicId, attempt, updateResult.ErrorCode, updateResult.Message);
                    return;
                }

                var delay = TimeSpan.FromMilliseconds(DeliveryRetryBaseMs * Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    "Transient update failure at {TPO} for {PublicId} (attempt {Attempt}/{Max}, ErrorCode={ErrorCode}): {Message}. Retrying in {Delay}.",
                    tpo, payload.ApplicationPublicId, attempt, MaxDeliveryAttempts,
                    updateResult.ErrorCode, updateResult.Message, delay);

                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= MaxDeliveryAttempts)
                {
                    _logger.LogCritical(ex,
                        "Dropping message at {TPO} for {PublicId} after {Attempts} unexpected failures.",
                        tpo, payload.ApplicationPublicId, attempt);
                    return;
                }

                var delay = TimeSpan.FromMilliseconds(DeliveryRetryBaseMs * Math.Pow(2, attempt - 1));
                _logger.LogWarning(ex,
                    "Unexpected update failure at {TPO} for {PublicId} (attempt {Attempt}/{Max}); retrying in {Delay}.",
                    tpo, payload.ApplicationPublicId, attempt, MaxDeliveryAttempts, delay);

                await Task.Delay(delay, ct);
            }
        }
    }

    // Errors that will never succeed on retry — commit and move on.
    private static bool IsTerminal(string? errorCode) =>
        errorCode == ErrorCodes.PORTAL.ApplicationNotFound ||
        errorCode == ErrorCodes.PORTAL.ApplicationAlreadyProtocoled;

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
}
