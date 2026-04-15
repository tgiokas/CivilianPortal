using CitizenPortal.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Interfaces;

public interface IApplicationService
{
    Task<Result<ApplicationSubmittedDto>> SubmitApplicationAsync(Guid keycloakUserId, ApplicationCreateDto request, List<IFormFile>? files, 
        CancellationToken cancellationToken = default);
    Task<Result<ApplicationDto>> GetApplicationAsync(Guid keycloakUserId, Guid publicId);
    Task<Result<PagedResult<ApplicationDto>>> GetApplicationsAsync(Guid keycloakUserId, ApplicationQueryParams queryParams);
    Task<Result<bool>> UpdateStatusFromDmsAsync(ProtocolAssignedEvent protocolEvent);
}
