namespace SuratGenerator.Infrastructure.Services;

internal static class PathSafety
{
    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static bool IsReservedName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var name = value.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return ReservedNames.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasInvalidFileNameChars(string value) =>
        value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

    public static bool HasTrailingDotOrSpace(string value) =>
        value.EndsWith('.') || value.EndsWith(' ');

    public static string NormalizeRoot(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static bool IsWithin(string root, string candidate, bool allowEqual)
    {
        var normalizedRoot = NormalizeRoot(root);
        var normalizedCandidate = NormalizeRoot(candidate);
        var relative = Path.GetRelativePath(normalizedRoot, normalizedCandidate);
        if (relative == ".")
        {
            return allowEqual;
        }

        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    public static string SanitizeName(
        string value,
        char replacement,
        int maxLength,
        bool collapseHyphenSeparators)
    {
        var text = (value ?? string.Empty).Trim();
        if (collapseHyphenSeparators)
        {
            text = text.Replace(" - ", " ");
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(Array.IndexOf(invalidChars, character) >= 0 ? replacement : character);
        }

        text = builder.ToString();
        if (collapseHyphenSeparators)
        {
            text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        text = text.Trim().TrimEnd('.', ' ');
        if (IsReservedName(text))
        {
            text = $"_{text}";
        }

        if (text.Length > maxLength)
        {
            text = text[..maxLength].TrimEnd('.', ' ');
        }

        return string.IsNullOrWhiteSpace(text) ? "Tanpa Nama" : text;
    }
}
