namespace SuratGenerator.Core.Models;

public sealed record BackupEntry(string Path, long SizeBytes, string Sha256);

public sealed record BackupProjectInfo(Guid Id, string Name, string FolderName);

public sealed record BackupManifest(
    string FormatVersion,
    DateTimeOffset CreatedAt,
    string Kind,
    IReadOnlyList<BackupEntry> Entries,
    IReadOnlyList<BackupProjectInfo> Projects,
    string? Notes = null);
