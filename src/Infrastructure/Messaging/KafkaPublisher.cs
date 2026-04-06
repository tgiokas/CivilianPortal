using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Confluent.Kafka;

using CitizenPortal.Application.Configuration;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.Messaging;

public sealed class KafkaPublisher : IMessagePublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IOptions<KafkaSettings> kafkaOptions, ILogger<KafkaPublisher> logger)
    {
        _logger = logger;
        var settings = kafkaOptions.Value;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            Acks = Enum.Parse<Acks>(settings.Acks),
            EnableIdempotence = settings.EnableIdempotence,
            MessageSendMaxRetries = settings.MessageSendMaxRetries,
            RetryBackoffMs = settings.RetryBackoffMs,
            RequestTimeoutMs = settings.RequestTimeoutMs,
            MessageTimeoutMs = settings.MessageTimeoutMs,
            ReconnectBackoffMs = settings.ReconnectBackoffMs,
            ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,
            SocketConnectionSetupTimeoutMs = settings.SocketConnectionSetupTimeoutMs,
            SocketTimeoutMs = settings.SocketTimeoutMs
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        _logger.LogInformation(
            "Kafka producer initialized — Servers: {Servers}, Acks: {Acks}, Idempotent: {Idempotent}",
            settings.BootstrapServers, settings.Acks, settings.EnableIdempotence);
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
