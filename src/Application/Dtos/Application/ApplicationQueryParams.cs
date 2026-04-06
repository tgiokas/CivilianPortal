using CitizenPortal.Domain.Enums;

namespace CitizenPortal.Application.Dtos.App;

public class ApplicationQueryParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public ApplicationStatus? Status { get; set; }
}
