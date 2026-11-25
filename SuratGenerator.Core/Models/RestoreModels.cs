namespace SuratGenerator.Core.Models;

public sealed record RestoreRequest(
    string ArchivePath,
    bool IncludeAllProjects = true,
    IReadOnlyList<Guid>? ProjectIds = null,
    bool IncludeDatabase = true,
    bool IncludeConfiguration = true,
    bool IncludeTemplates = true,
    bool IncludeMasterDocuments = true);

public sealed record RestoreResult(
    bool RequiresRestart,
    string Message,
    BackupPreview Preview,
    string? PreRestoreBackupPath = null);
