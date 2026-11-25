namespace SuratGenerator.Core.Models;

public sealed record BackupRequest(
    string DestinationDirectory,
    string Kind,
    bool IncludeAllProjects,
    IReadOnlyList<Guid> ProjectIds,
    string? Notes = null);

public sealed record BackupProgressInfo(string Stage, string Message, int Percent);

public sealed record BackupResult(BackupRecordInfo Record, string ArchivePath, BackupManifest Manifest);
