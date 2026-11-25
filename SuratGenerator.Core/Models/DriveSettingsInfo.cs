namespace SuratGenerator.Core.Models;

public sealed record DriveSettingsInfo(
    string FolderUrl,
    string FolderId,
    string? FolderName,
    string? AccountEmail,
    DateTimeOffset? LastSyncAt)
{
    public bool HasFolder => !string.IsNullOrWhiteSpace(FolderId);

    public bool IsSignedIn => !string.IsNullOrWhiteSpace(AccountEmail);

    public bool IsReady => HasFolder && IsSignedIn;

    public string FolderStatusText => HasFolder ? "Folder Google Drive tersambung" : "Folder Google Drive belum diatur";

    public string SignInStatusText => IsSignedIn ? $"Masuk sebagai {AccountEmail}" : "Belum masuk ke akun Google";

    public string LastSyncText => LastSyncAt is null
        ? "Belum pernah"
        : LastSyncAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm");
}
