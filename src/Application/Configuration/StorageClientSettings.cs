using Microsoft.Extensions.Configuration;

namespace CitizenPortal.Application.Configuration;

/// DMS.Storage HTTP client settings bound from environment variables.
/// Used to configure the HttpClient for file uploads.
public class StorageClientSettings
{
    // Base URL for DMS.Storage API (e.g. "http://dms-storage:8080")
    public string BaseUrl { get; set; } = string.Empty;

    public static StorageClientSettings BindFromConfiguration(IConfiguration configuration)
    {
        return new StorageClientSettings
        {
            BaseUrl = configuration["DMS_STORAGE_URL"]
                ?? throw new ArgumentNullException(nameof(configuration), "DMS_STORAGE_URL is not set.")
        };
    }
}
