namespace SuratGenerator.Core.Models;

public sealed record ProjectDocumentInfo(
    Guid Id,
    Guid ProjectId,
    Guid ProjectCategoryId,
    Guid? MasterDocumentId,
    Guid? MasterVersionId,
    string DisplayName,
    string FilePath,
    string FileType,
    string Source,
    string Status,
    string VersionSnapshot,
    bool IsRequired,
    long FileSizeBytes,
    DateTimeOffset? LastModifiedUtc,
    DateTimeOffset ImportedAt,
    DateTimeOffset UpdatedAt,
    string BaseTemplateFilePath = "",
    string EditedOutputFilePath = "",
    DateTimeOffset? LastEditedAt = null);
