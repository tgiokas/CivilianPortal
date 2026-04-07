namespace CitizenPortal.Application.Interfaces;

public record ErrorInfo(string Code, string Message);

public interface IErrorCatalog
{
    ErrorInfo GetError(string code);
}
