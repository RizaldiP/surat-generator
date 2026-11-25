namespace SuratGenerator.Core.Models;

public sealed record ProjectCreationResult(
    ProjectInfo? Project,
    bool IsDuplicate,
    Guid? ExistingProjectId,
    string RootPath,
    string Message,
    IReadOnlyList<string>? Warnings = null,
    int CopiedMasterCount = 0);
