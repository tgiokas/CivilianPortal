using System.Text.Json;
using System.Text.Json.Nodes;

namespace CitizenPortal.Infrastructure.Helpers.Redaction;

public static class JsonRedactor
{
    private const string RedactedValue = "***REDACTED***";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "newPassword", "pass",
        "refreshToken", "accessToken", "token", "idToken","loginToken","setupToken",
        "clientSecret", "code"
    };

  
    public static string TryRedact(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        try
        {
            var node = JsonNode.Parse(input);
            if (node is null) return input;

            RedactNode(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {          
            return input;
        }
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj.ToList())
            {
                if (kvp.Value is null) continue;

                if (SensitiveKeys.Contains(kvp.Key))
                    obj[kvp.Key] = RedactedValue;
                else
                    RedactNode(kvp.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null) RedactNode(item);
            }
                
        }
    }
}