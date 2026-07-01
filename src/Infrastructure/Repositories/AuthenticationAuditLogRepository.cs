using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.Database;

namespace CitizenPortal.Infrastructure.Repositories;

public class AuthenticationAuditLogRepository : IAuthenticationAuditLogRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AuthenticationAuditLogRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuthenticationAuditLog log, CancellationToken cancellationToken = default)
    {
        await _dbContext.AuthenticationAuditLogs.AddAsync(log, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
