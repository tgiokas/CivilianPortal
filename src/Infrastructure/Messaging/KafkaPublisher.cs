using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Confluent.Kafka;

using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.Messaging;

public sealed class KafkaPublisher : IMessagePublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IConfiguration config, ILogger<KafkaPublisher> logger)
    {
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["KAFKA_BOOTSTRAP_SERVERS"]
                ?? throw new ArgumentNullException(nameof(config), "KAFKA_BOOTSTRAP_SERVERS is not set."),

            Acks = Enum.Parse<Acks>(
                config["PORTAL_KAFKA_ACKS"] ?? "All"),

            EnableIdempotence = bool.Parse(
                config["PORTAL_KAFKA_ENABLE_IDEMPOTENCE"] ?? "true"),

            MessageSendMaxRetries = int.Parse(
                config["PORTAL_KAFKA_MESSAGE_SEND_MAX_RETRIES"] ?? "3"),

            RetryBackoffMs = int.Parse(
                config["PORTAL_KAFKA_RETRY_BACKOFF_MS"] ?? "100"),

            RequestTimeoutMs = int.Parse(
                config["PORTAL_KAFKA_REQUEST_TIMEOUT_MS"] ?? "5000"),

            MessageTimeoutMs = int.Parse(
                config["PORTAL_KAFKA_MESSAGE_TIMEOUT_MS"] ?? "10000")
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishJsonAsync<T>(
        string route,
        string key,
        T payload,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);

            var msg = new Message<string, string>
            {
                Key = key ?? string.Empty,
                Value = json,
                Headers = new Headers()
            };

            if (headers is not null)
            {
                foreach (var h in headers)
                    msg.Headers.Add(h.Key, System.Text.Encoding.UTF8.GetBytes(h.Value));
            }

            var result = await _producer.ProduceAsync(route, msg, cancellationToken);
            _logger.LogDebug("Produced to {TP} (offset {Offset})", result.TopicPartition, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Kafka produce error: {Reason}", ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        try { _producer.Flush(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error flushing Kafka producer during dispose"); }
        finally { _producer.Dispose(); }
    }
}
