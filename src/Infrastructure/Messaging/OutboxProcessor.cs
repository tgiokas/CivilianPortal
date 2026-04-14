using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Interfaces;
using CitizenPortal.Domain.Interfaces;

namespace CitizenPortal.Infrastructure.Messaging;


/// Background worker that polls the OutboxMessages table and publishes
/// pending messages to Kafka. This completes the Outbox Pattern:
/// 
/// 1. ApplicationService saves Application + OutboxMessage in one transaction
/// 2. OutboxProcessor picks up pending OutboxMessages
/// 3. Publishes each to Kafka
/// 4. Marks as processed (or increments retry on failure)
/// 
/// If Kafka is down, messages accumulate in the outbox and get retried.
/// If the service restarts, unprocessed messages are picked up again.

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IMessagePublisher publisher,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started. Polling every {Interval}s", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxProcessor encountered an error");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var pendingMessages = await outboxRepo.GetPendingAsync(batchSize: 20);

        if (pendingMessages.Count == 0)
            return;

        _logger.LogDebug("Processing {Count} outbox messages", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                var headers = new[]
                {
                    new KeyValuePair<string, string>("content-type", "application/json"),
                    new KeyValuePair<string, string>("x-event-id", message.EventId.ToString()),
                    new KeyValuePair<string, string>("x-event-type", message.EventType)
                };

                await _publisher.PublishJsonAsync(
                    route: message.EventType,
                    key: message.Key ?? message.EventId.ToString(),
                    payload: message.Payload,  // Already JSON-serialized
                    headers: headers,
                    cancellationToken: cancellationToken);

                await outboxRepo.MarkAsProcessedAsync(message.Id);

                _logger.LogDebug("Outbox message {EventId} published to {Topic}",
                    message.EventId, message.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish outbox message {EventId} (retry {Retry})",
                    message.EventId, message.RetryCount);

                await outboxRepo.MarkAsFailedAsync(message.Id, ex.Message);
            }
        }
    }
}
