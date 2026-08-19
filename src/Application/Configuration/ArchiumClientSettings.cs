using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// ARCHIUM BackEnd API settings bound from environment variables.
/// Used to configure the outbound HttpClient for folder browsing/creation and file retrieval.
public class ArchiumClientSettings
{
    // Base URL for ARCHIUM's BackEnd API
    public string BaseUrl { get; set; } = string.Empty;
    // Sent as the roleId header on folder-update requests.
    public string RoleId { get; set; } = string.Empty;
    // Sent as the organizationUnitId header on folder-update requests.
    public string OrganizationUnitId { get; set; } = string.Empty;

    public static ArchiumClientSettings BindFromConfiguration(IConfiguration configuration)
    {
        return new ArchiumClientSettings
        {
            BaseUrl = configuration["DMS_BACKEND_URL"]
                        ?? throw new ArgumentNullException(nameof(configuration), "DMS_BACKEND_URL is not set."),

            RoleId = configuration["ARCHIUM_ROLE_ID"] 
                        ?? throw new ArgumentNullException(nameof(configuration), "ARCHIUM_ROLE_ID is not set."),

            OrganizationUnitId = configuration["ARCHIUM_ORGANIZATION_UNIT_ID"] 
                        ?? throw new ArgumentNullException(nameof(configuration), "ARCHIUM_ORGANIZATION_UNIT_ID is not set.")
            
        };
    }
}
