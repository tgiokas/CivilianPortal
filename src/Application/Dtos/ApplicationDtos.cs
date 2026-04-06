using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Application.Dtos;

// === Request DTOs ===

public class ApplicationCreateDto
{
    public required string Subject { get; set; }
    public required string Email { get; set; }
    public required string Body { get; set; }
    // Files come as IFormFile in the controller, not here
}

public class ApplicationQueryParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public ApplicationStatus? Status { get; set; }
}

// === Response DTOs ===

public class ApplicationDto
{
    public Guid PublicId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProtocolNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ApplicationDocumentDto> Documents { get; set; } = [];
}

public class ApplicationDocumentDto
{
    public string StorageFileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class ApplicationSubmittedDto
{
    public Guid TrackingId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class CitizenUserDto
{
    public Guid KeycloakUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// === Kafka Event DTOs ===

public class ApplicationSubmittedEvent
{
    public Guid ApplicationPublicId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CitizenTaxisNetId { get; set; }
    public List<string> StorageFileIds { get; set; } = [];
    public DateTime SubmittedAt { get; set; }
}

public class ProtocolAssignedEvent
{
    public Guid ApplicationPublicId { get; set; }
    public string ProtocolNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
