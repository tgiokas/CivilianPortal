using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface IOutboxRepository
{   
    Task<List<OutboxMessage>> GetPendingAsync(int batchSize = 20);
    Task AddAsync(OutboxMessage message);
    Task MarkAsProcessedAsync(int id);
    Task MarkAsFailedAsync(int id, string error);
}
