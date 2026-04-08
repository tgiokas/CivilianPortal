using CitizenPortal.Application.Dtos;
using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Application.Errors;

public static class ErrorCatalogExtensions
{
    public static Result<T> Fail<T>(this IErrorCatalog errors, string code)
    {
        var e = errors.GetError(code);
        return Result<T>.Fail(e.Message, e.Code);
    }
}

