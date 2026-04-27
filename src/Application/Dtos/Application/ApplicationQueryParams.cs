using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Application.Dtos;

public class ApplicationQueryParams
{
    public required Guid KeycloakUserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;   
}
