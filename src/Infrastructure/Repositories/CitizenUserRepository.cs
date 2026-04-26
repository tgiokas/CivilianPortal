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

    public async Task<CitizenUser?> GetByEmailAsync(string email)
    {
        return await _dbContext.CitizenUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<CitizenUser?> GetByIdAsync(int id)
    {
        return await _dbContext.CitizenUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
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