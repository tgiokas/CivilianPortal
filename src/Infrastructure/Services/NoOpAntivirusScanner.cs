using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Infrastructure.Services;

/// Pass-through placeholder until a real ClamAV integration is wired in.
public class NoOpAntivirusScanner : IAntivirusScanner
{
    public Task<bool> IsCleanAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
