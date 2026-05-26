using CitizenPortal.Application.Dtos;

namespace CitizenPortal.Application.Interfaces;

public interface IEmailSender
{
    Task<bool> SendEmailAsync(NotificationEmailDto notification, string topic, CancellationToken cancellationToken = default);
}