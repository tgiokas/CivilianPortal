namespace CitizenPortal.Application.Interfaces;

/// Scans file content before it is archived (spec 3.9). No real ClamAV integration
/// exists yet — see NoOpAntivirusScanner for the current pass-through implementation.
public interface IAntivirusScanner
{
    Task<bool> IsCleanAsync(byte[] content, string fileName, CancellationToken cancellationToken = default);
}
