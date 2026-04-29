using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Confluent.Kafka;

using CitizenPortal.Application.Configuration;
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

                        await appService.UpdateStatusFromDmsAsync(payload);
                    }

                    _consumer.Commit(result); // success

                    _logger.LogInformation("Offset commited at {TPO} ", result.TopicPartitionOffset);
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
            try { _consumer.Close(); } // leave group & commit last offsets
            catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
        }

    }

    private static ProtocolAssignedEvent? ParsePayload(string payload)
    {
        // 1) Envelope with typed Content
        try
        {
            var env = JsonSerializer.Deserialize<KafkaMessage<ProtocolAssignedEvent>>(payload, JsonOpts);
            if (env?.Content is not null) return env.Content;
        }
        catch { /* fall through */ }

        // 2) Envelope with string Content
        try
        {
            var envRaw = JsonSerializer.Deserialize<KafkaMessage<string>>(payload, JsonOpts);
            if (!string.IsNullOrWhiteSpace(envRaw?.Content))
                return JsonSerializer.Deserialize<ProtocolAssignedEvent>(envRaw.Content, JsonOpts);
        }
        catch { /* fall through */ }

        // 3) Bare DTO
        try
        {
            return JsonSerializer.Deserialize<ProtocolAssignedEvent>(payload, JsonOpts);
        }
        catch { }

        return null;
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}