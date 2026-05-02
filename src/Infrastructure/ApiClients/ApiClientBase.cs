using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;

using CitizenPortal.Infrastructure.Helpers.Redaction;

namespace CitizenPortal.Infrastructure.ApiClients;

public abstract class ApiClientBase
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    const string LogMessageTemplate =
        "HTTP {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload} in {Elapsed:0.0000} ms";

    const string ErrorMessageTemplate =
        "ERROR {Direction} {RequestMethod} {RequestPath} {RequestPayload} responded {HttpStatusCode} {ResponsePayload}";

    protected ApiClientBase(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var contentType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;

        string requestBody;
        if (contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            requestBody = $"[{contentType}]";
        }
        else if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var requestBodyRaw = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            requestBody = FormUrlEncodedRedactor.TryRedact(requestBodyRaw);
        }
        else
        {
            requestBody = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
        }

        var sw = Stopwatch.StartNew();

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ErrorMessageTemplate, "Outgoing", request.Method,
                request.RequestUri, requestBody, HttpStatusCode.ServiceUnavailable, "");

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Service is temporarily unavailable.")
            };
        }

        sw.Stop();

        string responseBodyRaw = await response.Content.ReadAsStringAsync(cancellationToken);
        string responseBody = JsonRedactor.TryRedact(responseBodyRaw);

        int statusCode = (int)response.StatusCode;
        LogLevel logLevel = statusCode > 499 ? LogLevel.Error : LogLevel.Information;

        _logger.Log(logLevel, LogMessageTemplate, "Outgoing", request.Method,
            request.RequestUri, requestBody, statusCode, responseBody, (long)sw.ElapsedMilliseconds);

        return response;
    }
}
      
