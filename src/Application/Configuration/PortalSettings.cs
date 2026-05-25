using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// General CitizenPortal settings bound from environment variables.
/// Covers DB connection and frontend redirect URI.
public class PortalSettings
{
    /// Database
    public string DbConnection { get; set; } = string.Empty;

    /// Frontend redirect after OAuth2 callback
    public string FrontendRedirectUri { get; set; } = string.Empty;

    /// Browser origins allowed by CORS (comma-separated). When unset, only <see cref="FrontendRedirectUri"/> is used.
    public string[] CorsAllowedOrigins { get; set; } = [];

    public static PortalSettings BindFromConfiguration(IConfiguration configuration)
    {
        var redirect = configuration["FRONTEND_REDIRECTURI"]
            ?? throw new ArgumentNullException(nameof(configuration), "FRONTEND_REDIRECTURI is not set.");

        var corsExtra = configuration["FRONTEND_CORS_ORIGINS"];
        var origins = string.IsNullOrWhiteSpace(corsExtra)
            ? new[] { redirect.TrimEnd('/') }
            : corsExtra.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(o => o.TrimEnd('/'))
                .Where(o => o.Length > 0)
                .ToArray();

        return new PortalSettings
        {
            DbConnection = configuration["PORTAL_DB_CONNECTION"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_DB_CONNECTION is not set."),
            FrontendRedirectUri = redirect,
            CorsAllowedOrigins = origins
        };
    }
}
