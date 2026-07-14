namespace CitizenPortal.Application.Interfaces;

/// Scans file content before it is archived
/// No real ClamAV integration exists yet
public interface IAntivirusScanner
{
    Task<bool> IsCleanAsync(byte[] content, string fileName, CancellationToken cancellationToken = default);
}
