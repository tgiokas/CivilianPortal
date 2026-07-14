using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// ARCHIUM BackEnd API settings bound from environment variables.
/// Used to configure the outbound HttpClient for folder browsing/creation and file retrieval.
public class ArchiumClientSettings
{
    // Base URL for ARCHIUM's BackEnd API
    public string BaseUrl { get; set; } = string.Empty;

    // Sent as callerSystemId on every request per the interoperability spec.
    public string CallerSystemId { get; set; } = string.Empty;

    public static ArchiumClientSettings BindFromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["DMS_BACKEND_URL"]
            ?? throw new ArgumentNullException(nameof(configuration), "DMS_BACKEND_URL is not set.");       

        return new ArchiumClientSettings
        {
            BaseUrl = baseUrl,

            CallerSystemId = configuration["CALLER_SYSTEM_ID"]
                ?? throw new ArgumentNullException(nameof(configuration), "CALLER_SYSTEM_ID is not set.")
        };
    }
}
