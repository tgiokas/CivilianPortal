namespace CitizenPortal.Infrastructure.Helpers.Redaction;

public static class FormUrlEncodedRedactor
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "newPassword",
        "client_secret",
        "refresh_token",
        "access_token",
        "token",
        "id_token",
        "code",
        "loginToken",
        "setupToken"
    };

    public static string TryRedact(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        if (!input.Contains('='))
            return input;

        try
        {
            var pairs = input.Split('&', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < pairs.Length; i++)
            {
                var kv = pairs[i].Split('=', 2);
                if (kv.Length != 2) continue;

                var key = Uri.UnescapeDataString(kv[0]);
                if (SensitiveKeys.Contains(key))
                {
                    pairs[i] = $"{kv[0]}=***REDACTED***";
                }
            }

            return string.Join("&", pairs);
        }
        catch
        {
            return input;
        }
      
    }
}
