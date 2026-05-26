using Microsoft.Extensions.Logging;

using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.Messaging;

public class KafkaEmailSender : IEmailSender
{
    private readonly IMessagePublisher _kafkaPublisher;
    private readonly ILogger<KafkaEmailSender> _logger;

    public KafkaEmailSender(IMessagePublisher kafkaPublisher, ILogger<KafkaEmailSender> logger)
    {
        _kafkaPublisher = kafkaPublisher;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(
        NotificationEmailDto notification,
        string topic,
        CancellationToken cancellationToken = default)
    {
        var envelope = new KafkaMessage<NotificationEmailDto>
        {
            Id = Guid.NewGuid().ToString("N"),
            Content = notification,
            Timestamp = DateTime.UtcNow
        };

        var headers = new[]
        {
            new KeyValuePair<string, string>("content-type", "application/json"),
            new KeyValuePair<string, string>("x-channel", "email")
        };

        try
        {
            await _kafkaPublisher.PublishJsonAsync(
                route: topic,
                key: notification.Recipient,
                payload: envelope,
                headers: headers,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Email published to Kafka topic {Topic} for {Email}",
                topic, notification.Recipient);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish email to Kafka topic {Topic} for {Email}",
                topic, notification.Recipient);
            return false;
        }
    }
}
