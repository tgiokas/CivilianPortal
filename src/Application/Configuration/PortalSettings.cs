using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// <summary>
/// General CitizenPortal settings bound from environment variables.
/// Covers DB connection and frontend redirect URI.
/// </summary>
public class PortalSettings
{
    // Database
    public string DbConnection { get; set; } = string.Empty;

    // Frontend redirect after OAuth2 callback
    public string FrontendRedirectUri { get; set; } = string.Empty;

    public static PortalSettings BindFromConfiguration(IConfiguration configuration)
    {
        return new PortalSettings
        {
            DbConnection = configuration["PORTAL_DB_CONNECTION"]
                ?? throw new ArgumentNullException(nameof(configuration), "PORTAL_DB_CONNECTION is not set."),

            FrontendRedirectUri = configuration["FRONTEND_REDIRECTURI"]
                ?? throw new ArgumentNullException(nameof(configuration), "FRONTEND_REDIRECTURI is not set.")
        };
    }
}
