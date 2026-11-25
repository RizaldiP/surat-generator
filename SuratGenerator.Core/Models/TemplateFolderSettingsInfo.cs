namespace SuratGenerator.Core.Models;

public sealed record TemplateFolderSettingsInfo(
    string FolderPath,
    bool IncludeSubfolders,
    DateTimeOffset? LastScanAt)
{
    public bool HasFolder => !string.IsNullOrWhiteSpace(FolderPath);

    public string FolderStatusText => HasFolder
        ? $"Folder template: {FolderPath}"
        : "Folder template belum diatur";

    public string LastScanText => LastScanAt is null
        ? "Belum pernah"
        : LastScanAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm");
}
