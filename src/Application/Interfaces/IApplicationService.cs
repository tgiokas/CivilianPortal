using CitizenPortal.Application.Dtos;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace CitizenPortal.Application.Interfaces;

public interface IApplicationService
{
    Task<Result<ApplicationSubmittedDto>> SubmitApplicationAsync(ApplicationCreateDto request, List<IFormFile>? files,
        CancellationToken cancellationToken = default);
    Task<Result<ApplicationDto>> GetApplicationAsync(Guid publicId);
    Task<Result<PagedResult<ApplicationDto>>> GetApplicationsAsync(ApplicationQueryParams queryParams);
    Task<Result<bool>> UpdateStatusFromDmsAsync(ProtocolAssignedEvent protocolEvent);
}
