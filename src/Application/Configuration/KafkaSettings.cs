using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

public class KafkaSettings
{
    // Bootstrap servers
    public string BootstrapServers { get; set; } = string.Empty;

    // Base delay before reconnecting to a broker
    public int ReconnectBackoffMs { get; set; }

    // Maximum delay when exponential backoff applies
    public int ReconnectBackoffMaxMs { get; set; }   

    // Time allowed to establish initial TCP connection
    public int SocketConnectionSetupTimeoutMs { get; set; }

    // How long to wait for socket operations before failing
    public int SocketTimeoutMs { get; set; }


    // Producer     
    public string SubmittedTopic { get; set; } = string.Empty;    
    public string NotificationTopic { get; set; } = string.Empty;

    // Wait between retries to avoid hammering the broker
    public int RetryBackoffMs { get; set; }

    // Max time broker has to respond to produce request
    public int RequestTimeoutMs { get; set; }

    // Max time before message is considered failed (client side)
    public int MessageTimeoutMs { get; set; }


    // Consumer
    public string ProtocolTopic { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;

    // Offset reset strategy
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;

    // How long the broker waits for a heartbeat before removing consumer from group
    public int SessionTimeoutMs { get; set; }

    // Max time between Consume() calls before consumer is considered stuck
    public int MaxPollIntervalMs { get; set; }

    public static KafkaSettings BindFromConfiguration(IConfiguration configuration)
    {
        return new KafkaSettings
        {
            // Broker connection settings
            BootstrapServers = configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? throw new ArgumentNullException(nameof(configuration), "KAFKA_BOOTSTRAP_SERVERS is not set."),
            ReconnectBackoffMs = ParseInt(configuration, "PORTAL_KAFKA_RECONNECT_BACKOFF_MS"),
            ReconnectBackoffMaxMs = ParseInt(configuration, "PORTAL_KAFKA_RECONNECT_BACKOFF_MAX_MS"),
            SocketConnectionSetupTimeoutMs = ParseInt(configuration, "PORTAL_KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS"),
            SocketTimeoutMs = ParseInt(configuration, "PORTAL_KAFKA_SOCKET_TIMEOUT_MS"),

            // Producer settings
            SubmittedTopic = configuration["PORTAL_KAFKA_SUBMITTED_TOPIC"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_SUBMITTED_TOPIC is not set."),            
            NotificationTopic = configuration["PORTAL_KAFKA_NOTIFICATION_TOPIC"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_NOTIFICATION_TOPIC is not set."),
            RetryBackoffMs = ParseInt(configuration, "PORTAL_KAFKA_RETRY_BACKOFF_MS"),
            RequestTimeoutMs = ParseInt(configuration, "PORTAL_KAFKA_REQUEST_TIMEOUT_MS"),
            MessageTimeoutMs = ParseInt(configuration, "PORTAL_KAFKA_MESSAGE_TIMEOUT_MS"),

            // Consumer settings
            ProtocolTopic = configuration["PORTAL_KAFKA_PROTOCOL_TOPIC"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_PROTOCOL_TOPIC is not set."),
            ConsumerGroup = configuration["PORTAL_KAFKA_CONSUMER_GROUP"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_CONSUMER_GROUP is not set."),
            AutoOffsetReset = Enum.TryParse(configuration["PORTAL_KAFKA_AUTO_OFFSET_RESET"], true, out AutoOffsetReset offset)
                ? offset
                : throw new ArgumentException("PORTAL_KAFKA_AUTO_OFFSET_RESET is not a valid value.", nameof(configuration)),
            SessionTimeoutMs = ParseInt(configuration, "PORTAL_KAFKA_SESSION_TIMEOUT_MS"),
            MaxPollIntervalMs = ParseInt(configuration, "PORTAL_KAFKA_MAX_POLL_INTERVAL_MS"),
        };
    }

    private static int ParseInt(IConfiguration config, string key)
    {
        var raw = config[key]
            ?? throw new ArgumentNullException(nameof(config), $"{key} is not set.");
        if (!int.TryParse(raw, out var value))
            throw new ArgumentException($"{key} is not a valid integer.", nameof(config));
        return value;
    }
}
