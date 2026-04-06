using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message);
    Task<List<OutboxMessage>> GetPendingAsync(int batchSize = 20);
    Task MarkAsProcessedAsync(int id);
    Task MarkAsFailedAsync(int id, string error);
}
