using CitizenPortal.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace CitizenPortal.Application.Interfaces;

public interface IApplicationService
{
    Task<Result<ApplicationSubmittedDto>> SubmitApplicationAsync(
        ApplicationCreateDto request,
        List<IFormFile>? files,
        string externalSystem,
        CancellationToken cancellationToken = default);
    Task<Result<ApplicationDto>> GetApplicationAsync(Guid publicId);
    Task<Result<List<ApplicationDto>>> GetUserApplicationsAsync(CitizenUserIdDto queryParams);
    Task<Result<bool>> UpdateStatusFromDmsAsync(ProtocolAssignedEvent protocolEvent);
}
