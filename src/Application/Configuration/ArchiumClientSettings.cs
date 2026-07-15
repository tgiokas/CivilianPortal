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

    // Sent as the roleId header on folder-update requests.
    public string RoleId { get; set; } = string.Empty;

    // Sent as the organizationUnitId header on folder-update requests.
    public string OrganizationUnitId { get; set; } = string.Empty;

    // Sent as the Authorization: Bearer <token> header on folder-update requests.
    public string AuthToken { get; set; } = string.Empty;

    // Sent as the raw Cookie header on folder-update requests.
    public string Cookie { get; set; } = string.Empty;

    // metadataId sent on folder-update requests.
    public long MetadataId { get; set; } = 7;

    public static ArchiumClientSettings BindFromConfiguration(IConfiguration configuration)
    {
        var baseUrl = configuration["DMS_BACKEND_URL"]
            ?? throw new ArgumentNullException(nameof(configuration), "DMS_BACKEND_URL is not set.");

        return new ArchiumClientSettings
        {
            BaseUrl = baseUrl,

            CallerSystemId = configuration["CALLER_SYSTEM_ID"]
                ?? throw new ArgumentNullException(nameof(configuration), "CALLER_SYSTEM_ID is not set."),

            RoleId = configuration["ARCHIUM_ROLE_ID"] ?? string.Empty,
            OrganizationUnitId = configuration["ARCHIUM_ORGANIZATION_UNIT_ID"] ?? string.Empty,
            AuthToken = configuration["ARCHIUM_AUTH_TOKEN"] ?? string.Empty,
            Cookie = configuration["ARCHIUM_COOKIE"] ?? string.Empty,

            MetadataId = long.TryParse(configuration["ARCHIUM_METADATA_ID"], out var metadataId)
                ? metadataId
                : 7
        };
    }
}
