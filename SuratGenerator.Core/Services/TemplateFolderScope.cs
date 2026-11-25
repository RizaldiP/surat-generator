using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public static class TemplateFolderScope
{
    public static bool IsWithin(TemplateInfo template, TemplateFolderSettingsInfo settings)
    {
        if (!template.HasSource || !settings.HasFolder)
        {
            return false;
        }

        return IsWithinPath(template.SourceFilePath!, settings.FolderPath, settings.IncludeSubfolders);
    }

    public static bool IsWithinPath(string sourcePath, string folderPath, bool includeSubfolders)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        try
        {
            var comparison = StringComparison.OrdinalIgnoreCase;
            var root = NormalizeDirectory(folderPath);
            var rootWithSeparator = root + Path.DirectorySeparatorChar;
            var fullSourcePath = Path.GetFullPath(sourcePath);

            if (!includeSubfolders)
            {
                var parent = NormalizeDirectory(Path.GetDirectoryName(fullSourcePath) ?? string.Empty);
                return string.Equals(parent, root, comparison);
            }

            return fullSourcePath.StartsWith(rootWithSeparator, comparison);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
