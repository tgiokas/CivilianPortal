namespace CitizenPortal.Application.Dtos;

/// Carries HttpContext-only values (captured in the API layer) into the
/// Application layer so authentication calls can be audited without the
/// Application layer depending on HttpContext.
public sealed record AuthAuditContext(string? IpAddress, string? MachineName);
