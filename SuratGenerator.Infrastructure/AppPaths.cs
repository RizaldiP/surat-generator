namespace SuratGenerator.Infrastructure;

public static class AppPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SuratGenerator");

    public static string TemplatesDirectory => Path.Combine(DataDirectory, "Templates");

    public static string PreviewDirectory => Path.Combine(DataDirectory, "Preview");

    public static string DatabasePath => Path.Combine(DataDirectory, "surat-generator.db");

    public static string ExportDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "SuratGenerator");

    public static string OAuthClientPath => Path.Combine(DataDirectory, "oauth-client.json");

    public static string DriveSettingsPath => Path.Combine(DataDirectory, "drive-settings.json");

    public static string TemplateFolderSettingsPath => Path.Combine(DataDirectory, "template-folder.json");

    public static string WorkspaceSettingsPath => Path.Combine(DataDirectory, "workspace-paths.json");

    public static string DefaultMasterDocumentsDirectory => Path.Combine(DataDirectory, "MasterDocuments");

    public static string DefaultProjectsDirectory => Path.Combine(DataDirectory, "Projects");

    public static string DefaultBackupsDirectory => Path.Combine(DataDirectory, "Backups");

    public static string PendingRestoreDirectory => Path.Combine(DataDirectory, "PendingRestore");

    public static string PendingRestoreMarkerPath => Path.Combine(PendingRestoreDirectory, "pending-restore.json");

    public static string SecureDirectory => Path.Combine(DataDirectory, "secure");

    public static string DriveTokenPath => Path.Combine(SecureDirectory, "drive-token.bin");

    public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");

    public static string LogFilePath => Path.Combine(LogsDirectory, "surat-generator.log");
}
