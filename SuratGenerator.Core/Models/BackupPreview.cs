namespace SuratGenerator.Core.Models;

public sealed record BackupPreview(
    string ArchivePath,
    BackupManifest Manifest,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Conflicts,
    bool CanRestore);
