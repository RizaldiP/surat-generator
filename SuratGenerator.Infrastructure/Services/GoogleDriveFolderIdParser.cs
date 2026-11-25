using System.Text.RegularExpressions;

namespace SuratGenerator.Infrastructure.Services;

public static partial class GoogleDriveFolderIdParser
{
    public static bool TryExtract(string? input, out string folderId)
    {
        folderId = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();
        if (IsValid(value))
        {
            folderId = value;
            return true;
        }

        var match = FolderPattern().Match(value);
        if (match.Success && IsValid(match.Groups[1].Value))
        {
            folderId = match.Groups[1].Value;
            return true;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            foreach (var part in uri.Query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                var name = pair[0].TrimStart('?');
                if (pair.Length == 2 &&
                    string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) &&
                    IsValid(Uri.UnescapeDataString(pair[1])))
                {
                    folderId = Uri.UnescapeDataString(pair[1]);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsValid(string value) =>
        IdPattern().IsMatch(value);

    [GeneratedRegex(@"[/&?]folders[/=]([A-Za-z0-9_-]{10,})", RegexOptions.IgnoreCase)]
    private static partial Regex FolderPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{20,}$")]
    private static partial Regex IdPattern();
}
