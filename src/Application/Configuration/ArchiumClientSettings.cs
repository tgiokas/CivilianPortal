using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// ARCHIUM™ External-Portal API settings bound from environment variables.
/// Used to configure the outbound HttpClient for folder browsing/creation and file retrieval.
public class ArchiumClientSettings
{
    // Base URL for ARCHIUM's External-Portal API (e.g. "https://archium.example.gr")
    public string BaseUrl { get; set; } = string.Empty;

    // Sent as callerSystemId on every request per the interoperability spec.
    public string CallerSystemId { get; set; } = string.Empty;

    public static ArchiumClientSettings BindFromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["ARCHIUM_BASEURL"]
            ?? throw new ArgumentNullException(nameof(configuration), "ARCHIUM_BASEURL is not set.");

        // HTTPS-only per spec 3.2/3.9 — plain HTTP is only tolerated in local Development.
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        if (!environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ARCHIUM_BASEURL must use HTTPS outside Development.", nameof(configuration));
        }

        return new ArchiumClientSettings
        {
            BaseUrl = baseUrl,

            CallerSystemId = configuration["ARCHIUM_CALLER_SYSTEM_ID"]
                ?? throw new ArgumentNullException(nameof(configuration), "ARCHIUM_CALLER_SYSTEM_ID is not set.")
        };
    }
}
