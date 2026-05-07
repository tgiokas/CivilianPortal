using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;
using Confluent.Kafka;

namespace CitizenPortal.Application.Errors;

public static class ErrorCatalogExtensions
{
    public static Result<T> Fail<T>(this IErrorCatalog errors, string code)
    {
        var e = errors.GetError(code);
        return Result<T>.Fail(e.Message, e.Code);
    }

    public static Result<T> Fail<T>(this IErrorCatalog errors, List<string> codes)
    {
        var allErrors = codes.Select(errors.GetError);

        var message = string.Join("|", allErrors.Select(e => e.Message));
        var code = string.Join("|", allErrors.Select(e => e.Code));
        return Result<T>.Fail(message, code);
    }
}

