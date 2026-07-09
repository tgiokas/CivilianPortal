using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface IAuthenticationAuditLogRepository
{
    Task AddAsync(AuthenticationAuditLog log, CancellationToken cancellationToken = default);
}
