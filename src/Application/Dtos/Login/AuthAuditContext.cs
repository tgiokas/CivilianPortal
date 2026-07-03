namespace CitizenPortal.Application.Dtos;

/// Carries HttpContext-only values (captured in the API layer) into the
/// Application layer so authentication calls can be audited without the
/// Application layer depending on HttpContext.
/// IpAddress is the client (workstation) IP. MachineName is the workstation
/// name self-reported by the caller via the X-Machine-Name header (untrusted,
/// optional); the service falls back to the server host name when it is absent.
public sealed record AuthAuditContext(string? IpAddress, string? MachineName);
