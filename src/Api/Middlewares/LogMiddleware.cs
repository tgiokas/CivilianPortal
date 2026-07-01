using System.Diagnostics;
using CitizenPortal.Infrastructure.Helpers.Redaction;

namespace CitizenPortal.Api.Middlewares;

public class LogMiddleware
{
    private readonly RequestDelegate _next;
    private const int MaxBufferableBodyBytes = 1024 * 1024; // 1 MB
    private const int MaxPayloadLength = 4096;

    // Paths whose responses are streamed (file downloads, etc.) and must NOT be buffered. 
    private static readonly string[] BodyCaptureBypassPaths =
    {
        "/application/download",
    };

    const string LogMessageTemplate =
        "HTTP {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload} in {Elapsed:0.0000} ms";

    public LogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext, ILogger<LogMiddleware> logger)
    {
        // Redact request body (only JSON and small multipart bodies are read).
        string safeRequestBody = await BuildSafeRequestBodyAsync(httpContext.Request);
        safeRequestBody = Truncate(safeRequestBody, MaxPayloadLength);

        // Streaming endpoints (downloads): never swap the response body to a
        // MemoryStream, otherwise the whole file would be buffered in memory.
        // Run the pipeline untouched and log a placeholder for the response body.
        if (ShouldBypassBodyCapture(httpContext.Request.Path))
        {
            await InvokeWithoutCaptureAsync(httpContext, logger, safeRequestBody);
            return;
        }

        // Copy a pointer to the original response body stream
        Stream originalBodyStream = httpContext.Response.Body;

        // Create a new memory stream and use it for the temporary response body
        using var newMemoryStream = new MemoryStream();
        httpContext.Response.Body = newMemoryStream;

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(httpContext);
        }
        finally
        {
            sw.Stop();

            // Only JSON/text responses are read and redacted.
            // binary responses (e.g. file downloads) are summarised from headers, never buffered.
            string safeResponseBody = BuildSafeResponseBody(httpContext.Response);
            safeResponseBody = Truncate(safeResponseBody, MaxPayloadLength);

            WriteLog(logger, httpContext, safeRequestBody, safeResponseBody, sw);

            httpContext.Response.Body = originalBodyStream;

            if (!httpContext.Response.HasStarted)
            {
                newMemoryStream.Seek(0, SeekOrigin.Begin);
                await newMemoryStream.CopyToAsync(originalBodyStream);
            }
        }
    }

    // Runs the pipeline with the original response stream left in place so the
    // body streams directly to the client. The response body is not read.
    private async Task InvokeWithoutCaptureAsync(
        HttpContext httpContext, ILogger logger, string safeRequestBody)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(httpContext);
        }
        finally
        {
            sw.Stop();

            var contentType = httpContext.Response.ContentType ?? "unknown";
            var len = httpContext.Response.ContentLength?.ToString() ?? "?";
            string safeResponseBody = $"[{contentType}; {len} bytes — body not logged (streamed)]";

            WriteLog(logger, httpContext, safeRequestBody, safeResponseBody, sw);
        }
    }

    private static void WriteLog(
        ILogger logger, HttpContext httpContext,
        string safeRequestBody, string safeResponseBody, Stopwatch sw)
    {
        int statusCode = httpContext.Response.StatusCode;
        LogLevel loglevel = statusCode > 499 ? LogLevel.Error : LogLevel.Information;

        // Log using Serilog
        logger.Log(loglevel, LogMessageTemplate, "Incoming",
            httpContext.Request.Method, httpContext.Request.Path, safeRequestBody,
            statusCode, safeResponseBody, sw.Elapsed.TotalMilliseconds);
    }

    private static bool ShouldBypassBodyCapture(PathString path)
    {
        foreach (var bypass in BodyCaptureBypassPaths)
        {
            if (path.StartsWithSegments(bypass, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static async Task<string> BuildSafeRequestBodyAsync(HttpRequest request)
    {
        bool isJson = request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
        bool isForm = request.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;

        // Only JSON and form bodies & Get requests  are worth logging.
        // Anything else with an actual body (octet-stream, etc.) gets a placeholder.
        // A request with no body (e.g. GET) logs all the QueryString.
        if (!isJson && !isForm)
            return HasBody(request)
                ? DescribeBodyWithoutReading(request)
                : (request.QueryString.HasValue ? request.QueryString.Value!.TrimStart('?') : string.Empty);

        // Skip large or unknown-length (chunked) bodies - never buffer them just to log.
        if (request.ContentLength is not long len || len > MaxBufferableBodyBytes)
            return DescribeBodyWithoutReading(request);

        // Small enough to read: buffer, read, rewind for the controller.
        var body = await GetRequestBody(request);

        return isJson
            ? JsonRedactor.TryRedact(body)
            : MultipartFormDataRedactor.TryRedact(body);
    }

    private static async Task<string> GetRequestBody(HttpRequest request)
    {
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        string body = await reader.ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private static bool HasBody(HttpRequest request)
    {
        if (request.ContentLength is long len)
            return len > 0;
        return !string.IsNullOrEmpty(request.ContentType);
    }

    private static string DescribeBodyWithoutReading(HttpRequest request)
    {
        var contentType = request.ContentType ?? "unknown";
        return request.ContentLength is long len
            ? $"[{contentType}; {len} bytes — body not logged]"
            : $"[{contentType}; body not logged]";
    }

    private static string Truncate(string input, int maxLen)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLen) return input;
        return input.Substring(0, maxLen) + "...(truncated)";
    }

    private static string BuildSafeResponseBody(HttpResponse response)
    {
        var contentType = response.ContentType ?? "";

        bool isTextual =
            contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

        if (!isTextual)
        {
            var ct = string.IsNullOrEmpty(contentType) ? "unknown" : contentType;
            return $"[{ct}; {response.ContentLength?.ToString() ?? "?"} bytes — body not logged]";
     }
        
        if (response.Body.Length > MaxBufferableBodyBytes)
        {
            var ct = string.IsNullOrEmpty(contentType) ? "unknown" : contentType;
            return $"[{ct}; {response.Body.Length} bytes — body too large to log]";
               }

        // Read the buffered response, then rewind so it can still be copied to
        // the real response stream below (leaveOpen keeps the MemoryStream open).
        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        string body = reader.ReadToEnd();
        response.Body.Seek(0, SeekOrigin.Begin);

        return JsonRedactor.TryRedact(body);
    }
}