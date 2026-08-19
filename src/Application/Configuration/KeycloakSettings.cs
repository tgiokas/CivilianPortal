using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// Keycloak configuration bound from environment variables.
/// Used for JWT Bearer validation in Program.cs and for
/// token exchange in KeycloakClientAuthentication.
public class KeycloakSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string CitizenRealm { get; set; } = string.Empty;
    public string DmsRealm { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string OpsClientId { get; set; } = string.Empty;    
    public string OpsDmsClientId { get; set; } = string.Empty;
    public string OpsDmsClientSecret { get; set; } = string.Empty;
    public string OpsDmsUser { get; set; } = string.Empty;
    public string OpsDmsPassword { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = false;

    // Audiences accepted by the JWT Bearer handler: the portal client plus, when configured,
    // the OPS service-to-service client. Blank entries are dropped so environments without
    // KEYCLOAK_OPS_CLIENTID set keep validating against ClientId alone.
    public IEnumerable<string> ValidAudiences =>
        new[] { ClientId, OpsClientId }.Where(a => !string.IsNullOrWhiteSpace(a));

    public static KeycloakSettings BindFromConfiguration(IConfiguration configuration)
    {
        return new KeycloakSettings
        {
            BaseUrl = configuration["KEYCLOAK_BASEURL"]
                ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_BASEURL is not set."),

            CitizenRealm = configuration["KEYCLOAK_PORTAL_REALM"]
                ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_PORTAL_REALM is not set."),

            DmsRealm = configuration["KEYCLOAK_DMS_REALM"]
                        ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_DMS_REALM is not set."),

            Authority = configuration["KEYCLOAK_PORTAL_AUTHORITY"]
                ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_PORTAL_AUTHORITY is not set."),

            ClientId = configuration["KEYCLOAK_PORTAL_CLIENTID"]
                ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_PORTAL_CLIENTID is not set."),

            ClientSecret = configuration["KEYCLOAK_PORTAL_CLIENTSECRET"]
                ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_PORTAL_CLIENTSECRET is not set."),

            OpsClientId = configuration["KEYCLOAK_OPS_CLIENTID"]
                ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_OPS_CLIENTID is not set."),

            OpsDmsClientId = configuration["OPS_DMS_CLIENT_ID"]
                        ?? throw new ArgumentNullException(nameof(configuration), "OPS_DMS_CLIENT_ID is not set."),

            OpsDmsClientSecret = configuration["OPS_DMS_CLIENT_SECRET"]
                        ?? throw new ArgumentNullException(nameof(configuration), "OPS_DMS_CLIENT_SECRET is not set."),

            OpsDmsUser = configuration["OPS_DMS_USERNAME"]
                        ?? throw new ArgumentNullException(nameof(configuration), "OPS_DMS_USERNAME is not set."),

            OpsDmsPassword = configuration["OPS_DMS_PASSWORD"]
                        ?? throw new ArgumentNullException(nameof(configuration), "OPS_DMS_PASSWORD is not set."),

            RedirectUri = configuration["KEYCLOAK_PORTAL_REDIRECTURI"]
                ?? throw new ArgumentNullException(nameof(configuration), "KEYCLOAK_PORTAL_REDIRECTURI is not set."),

            RequireHttpsMetadata = bool.Parse(
                configuration["KEYCLOAK_REQUIRE_HTTPS_METADATA"] ?? "false"),            
        };
    }
}
