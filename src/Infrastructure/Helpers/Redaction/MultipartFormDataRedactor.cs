using System.Text.RegularExpressions;

namespace CitizenPortal.Infrastructure.Helpers.Redaction;

public static class MultipartFormDataRedactor
{
    private const string RedactedValue = "***REDACTED***";

    // Scalar fields whose *value* should be redacted even when they aren't files.
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "token", "code"
    };

    // Matches name=file or name="file"
    private static readonly Regex NameRegex =
        new(@"name=""?(?<name>[^"";\r\n]+)""?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string TryRedact(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        try
        {
            // First line of a multipart body is the boundary.
            var firstLineEnd = input.IndexOf('\n');
            if (firstLineEnd < 0) return input;

            var boundary = input.Substring(0, firstLineEnd).Trim();
            if (boundary.Length == 0) return input;

            var parts = input.Split(boundary, StringSplitOptions.None);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (string.IsNullOrWhiteSpace(part) || !part.Contains("Content-Disposition"))
                    continue;

                var nameMatch = NameRegex.Match(part);
                var name = nameMatch.Success ? nameMatch.Groups["name"].Value.Trim() : null;

                // Redact the body of any file part, or any sensitive scalar field.
                bool isFilePart = part.Contains("filename", StringComparison.OrdinalIgnoreCase);
                bool isSensitive = name is not null && SensitiveKeys.Contains(name);

                if (!isFilePart && !isSensitive)
                    continue;

                // Keep the headers (Content-Disposition carries the filename),
                // replace only the part body.
                var headerEnd = part.IndexOf("\r\n\r\n");
                var sepLength = 4;
                if (headerEnd < 0)
                {
                    headerEnd = part.IndexOf("\n\n");
                    sepLength = 2;
                }
                if (headerEnd >= 0)
                {
                    var headers = part.Substring(0, headerEnd + sepLength);
                    parts[i] = headers + RedactedValue + "\r\n";
                }
            }

            return string.Join(boundary, parts);
        }
        catch
        {
            return input;
        }
    }
}