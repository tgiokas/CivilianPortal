using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CitizenPortal.Application.Errors;
using CitizenPortal.Application.Interfaces;
using CitizenPortal.Application.Constants;

namespace CitizenPortal.Api.Filters;

// Rejects oversized upload requests by Content-Length before model binding reads the body.
// Runs as a resource filter (ahead of model binding), so this app-level limit trips before
// Kestrel's own MaxRequestBodySize (Program.cs, set higher as a buffer) — the client
// gets the app's Result{T} error contract instead of the framework's default 400 ProblemDetails.
public class FileSizeLimitFilter : IAsyncResourceFilter
{
    private readonly IErrorCatalog _errors;

    public FileSizeLimitFilter(IErrorCatalog errors)
    {
        _errors = errors;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.HttpContext.Request.ContentLength is long contentLength && contentLength > SizeLimitsConstants.MaxUploadBytes)
        {
            var result = _errors.Fail<object>(ErrorCodes.PORTAL.ArchiveFileTooLarge);
            context.Result = new ObjectResult(result) { StatusCode = StatusCodes.Status413PayloadTooLarge };
            return;
        }

        await next();
    }
}
