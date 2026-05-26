using CitizenPortal.Domain.Entities;

namespace CitizenPortal.Domain.Interfaces;

public interface IOutboxRepository
{
    Task<List<OutboxMessage>> GetPendingAsync(int batchSize = 20, CancellationToken cancellationToken = default);
    Task AddAsync(OutboxMessage message);
    Task MarkAsProcessedAsync(int id, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(int id, string error, CancellationToken cancellationToken = default);
}
