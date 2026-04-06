using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// <summary>
/// Kafka configuration bound from environment variables.
/// Covers producer tuning, consumer group, and CitizenPortal-specific topics.
/// Follows the same pattern as Authentication.Application.Configuration.KafkaSettings.
/// </summary>
public class KafkaSettings
{
    // Bootstrap servers
    public string BootstrapServers { get; set; } = string.Empty;

    // Topics
    public string SubmittedTopic { get; set; } = string.Empty;
    public string ProtocolTopic { get; set; } = string.Empty;
    public string NotificationTopic { get; set; } = string.Empty;

    // Consumer
    public string ConsumerGroup { get; set; } = string.Empty;

    // Durability / Acknowledgement
    public string Acks { get; set; } = "All";

    // Base delay before reconnecting to a broker
    public int ReconnectBackoffMs { get; set; }

    // Maximum delay when exponential backoff applies
    public int ReconnectBackoffMaxMs { get; set; }

    // Time allowed to establish initial TCP connection
    public int SocketConnectionSetupTimeoutMs { get; set; }

    // How long to wait for socket operations before failing
    public int SocketTimeoutMs { get; set; }

    // How many times the .NET client retries failed sends
    public int MessageSendMaxRetries { get; set; }

    // Wait between retries to avoid hammering the broker
    public int RetryBackoffMs { get; set; }

    // Max time broker has to respond to produce request
    public int RequestTimeoutMs { get; set; }

    // Max time before message is considered failed (client side)
    public int MessageTimeoutMs { get; set; }

    // Enabling idempotent producers
    public bool EnableIdempotence { get; set; }

    public static KafkaSettings BindFromConfiguration(IConfiguration configuration)
    {
        return new KafkaSettings
        {
            BootstrapServers = configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? throw new ArgumentNullException(nameof(configuration), "KAFKA_BOOTSTRAP_SERVERS is not set."),

            SubmittedTopic = configuration["PORTAL_KAFKA_SUBMITTED_TOPIC"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_SUBMITTED_TOPIC is not set."),

            ProtocolTopic = configuration["PORTAL_KAFKA_PROTOCOL_TOPIC"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_PROTOCOL_TOPIC is not set."),

            NotificationTopic = configuration["PORTAL_KAFKA_NOTIFICATION_TOPIC"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_NOTIFICATION_TOPIC is not set."),

            ConsumerGroup = configuration["PORTAL_KAFKA_CONSUMER_GROUP"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_CONSUMER_GROUP is not set."),

            Acks = configuration["PORTAL_KAFKA_ACKS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_ACKS is not set."),

            ReconnectBackoffMs = int.Parse(
                configuration["PORTAL_KAFKA_RECONNECT_BACKOFF_MS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_RECONNECT_BACKOFF_MS is not set.")),

            ReconnectBackoffMaxMs = int.Parse(
                configuration["PORTAL_KAFKA_RECONNECT_BACKOFF_MAX_MS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_RECONNECT_BACKOFF_MAX_MS is not set.")),

            SocketConnectionSetupTimeoutMs = int.Parse(
                configuration["PORTAL_KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS is not set.")),

            SocketTimeoutMs = int.Parse(
                configuration["PORTAL_KAFKA_SOCKET_TIMEOUT_MS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_SOCKET_TIMEOUT_MS is not set.")),

            MessageSendMaxRetries = int.Parse(
                configuration["PORTAL_KAFKA_MESSAGE_SEND_MAX_RETRIES"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_MESSAGE_SEND_MAX_RETRIES is not set.")),

            RetryBackoffMs = int.Parse(
                configuration["PORTAL_KAFKA_RETRY_BACKOFF_MS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_RETRY_BACKOFF_MS is not set.")),

            RequestTimeoutMs = int.Parse(
                configuration["PORTAL_KAFKA_REQUEST_TIMEOUT_MS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_REQUEST_TIMEOUT_MS is not set.")),

            MessageTimeoutMs = int.Parse(
                configuration["PORTAL_KAFKA_MESSAGE_TIMEOUT_MS"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_MESSAGE_TIMEOUT_MS is not set.")),

            EnableIdempotence = bool.Parse(
                configuration["PORTAL_KAFKA_ENABLE_IDEMPOTENCE"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_KAFKA_ENABLE_IDEMPOTENCE is not set."))
        };
    }
}
