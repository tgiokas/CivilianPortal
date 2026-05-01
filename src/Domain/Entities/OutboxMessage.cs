namespace CitizenPortal.Domain.Entities;

/// Outbox Pattern: Events are written to this table in the same DB transaction 
/// as the domain data. A background worker picks them up and publishes to Kafka.
/// This guarantees: both DB insert and Kafka message succeed or both fail.
public class OutboxMessage
{
    public int Id { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;     // e.g. "citizen.application.submitted"
    public string Payload { get; set; } = string.Empty;       // JSON serialized event data
    public string? Key { get; set; }                          // Kafka partition key
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }                // null = pending, set = published
    public DateTime? LastAttemptAt { get; set; }             // When the last publish attempt was made
    public int RetryCount { get; set; } = 0;
    public string? Error { get; set; }                        // Last error if any
}
