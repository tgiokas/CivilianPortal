using Microsoft.EntityFrameworkCore;

using Npgsql;

using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.Database;

namespace CitizenPortal.Infrastructure.Repositories;

public class CitizenUserRepository : ICitizenUserRepository
{
    // Postgres SQLSTATE for unique_violation. Anything else from
    // DbUpdateException is a real failure and must propagate.
    private const string UniqueViolationSqlState = "23505";

    private readonly ApplicationDbContext _dbContext;

    public CitizenUserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CitizenUser?> GetByKeycloakUserIdReadOnlyAsync(Guid keycloakUserId)
    {
        return await _dbContext.CitizenUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId);
    }

    public async Task<CitizenUser?> GetByKeycloakUserIdAsync(Guid keycloakUserId)
    {
        return await _dbContext.CitizenUsers
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId);
    }

    public async Task<(CitizenUser User, bool Created)> GetOrCreateAsync(CitizenUser newUser)
    {
        await _dbContext.CitizenUsers.AddAsync(newUser);
        try
        {
            await _dbContext.SaveChangesAsync();
            return (newUser, true);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
                                           && pg.SqlState == UniqueViolationSqlState)
        {
            // A concurrent request inserted the same citizen between our read and write.
            // Detach the failed entity so the change tracker is clean, then re-fetch.
            _dbContext.Entry(newUser).State = EntityState.Detached;

            var existing = await _dbContext.CitizenUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.KeycloakUserId == newUser.KeycloakUserId);

            if (existing is not null)
                return (existing, false);

            throw;
        }
    }

    public async Task UpdateAsync(CitizenUser user, CancellationToken cancellationToken = default)
    {
        user.ModifiedAt = DateTime.UtcNow;

        // Update() handles both tracked entities (already modified) and
        // untracked entities (e.g. the lost-race branch from GetOrCreateAsync
        // returns AsNoTracking) by attaching the entity in the Modified state.
        _dbContext.CitizenUsers.Update(user);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}