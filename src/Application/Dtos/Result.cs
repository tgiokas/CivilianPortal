namespace CitizenPortal.Application.Dtos;

public class Result<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public T? Data { get; set; }

    public static Result<T> Ok(T data, string? message = null) =>
        new()
        {
            Success = true,
            Data = data,
            Message = message
        };

    public static Result<T> Fail(string message, string? errorCode = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode
        };
}
