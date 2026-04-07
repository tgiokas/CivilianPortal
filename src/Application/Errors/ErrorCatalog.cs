using CitizenPortal.Application.Interfaces;

namespace CitizenPortal.Application.Errors;

public class ErrorCatalog : IErrorCatalog
{
    private record ErrorEntry(string Message);
    private readonly Dictionary<string, ErrorEntry> _map;

    private ErrorCatalog(Dictionary<string, ErrorEntry> map)
    {
        _map = map;
    }

    public static IErrorCatalog LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var map = new Dictionary<string, ErrorEntry>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("PORTAL", out var portalArray) && 
            portalArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in portalArray.EnumerateArray())
            {
                var code = item.GetProperty("code").GetString();
                var msg = item.GetProperty("message").GetString() ?? "An error occurred";
                if (!string.IsNullOrWhiteSpace(code))
                {
                    map[code] = new ErrorEntry(msg);
                }
            }
        }
        return new ErrorCatalog(map);
    }

    public ErrorInfo GetError(string code)
    {
        if (_map.TryGetValue(code, out var e))
            return new ErrorInfo(code, e.Message);

        const string Fallback = "PORTAL-000";
        if (_map.TryGetValue(Fallback, out var f))
            return new ErrorInfo(Fallback, f.Message);

        return new ErrorInfo(code, "An unexpected error occurred.");
    }
}