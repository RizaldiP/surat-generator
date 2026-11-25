namespace SuratGenerator.Infrastructure;

public sealed record BackupPaths(
    string DatabasePath,
    string TemplatesDirectory,
    IReadOnlyList<string> ConfigFilePaths,
    string PendingRestoreDirectory,
    string PendingRestoreMarkerPath)
{
    public static BackupPaths Production => new(
        AppPaths.DatabasePath,
        AppPaths.TemplatesDirectory,
        new[]
        {
            AppPaths.WorkspaceSettingsPath,
            AppPaths.DriveSettingsPath,
            AppPaths.TemplateFolderSettingsPath
        },
        AppPaths.PendingRestoreDirectory,
        AppPaths.PendingRestoreMarkerPath);

    public string RestoreErrorPath => Path.Combine(PendingRestoreDirectory, "restore-error.txt");
}
