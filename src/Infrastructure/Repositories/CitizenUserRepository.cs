using Microsoft.EntityFrameworkCore;

using CitizenPortal.Domain.Entities;
using CitizenPortal.Domain.Interfaces;
using CitizenPortal.Infrastructure.Database;

namespace CitizenPortal.Infrastructure.Repositories;

public class CitizenUserRepository : ICitizenUserRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CitizenUserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CitizenUser?> GetByIdAsync(int id)
    {
        return await _dbContext.CitizenUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
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
        catch (DbUpdateException)
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

    public async Task AddAsync(CitizenUser user)
    {
        await _dbContext.CitizenUsers.AddAsync(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(CitizenUser user)
    {
        user.ModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }
}