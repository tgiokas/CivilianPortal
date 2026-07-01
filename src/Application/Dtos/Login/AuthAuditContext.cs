namespace CitizenPortal.Application.Dtos;

/// Carries HttpContext-only values (captured in the API layer) into the
/// Application layer so authentication calls can be audited without the
/// Application layer depending on HttpContext.
/// IpAddress is the client (workstation) IP; the server machine name is
/// resolved by the service from the host itself.
public sealed record AuthAuditContext(string? IpAddress);
