namespace SuratGenerator.Core.Models;

public sealed record MasterDocumentInfo(
    Guid Id,
    Guid CategoryId,
    string DisplayName,
    string SourceFileName,
    string StoredFilePath,
    string FileType,
    bool IsRequired,
    bool IsActive,
    bool IsArchived,
    Guid? CurrentVersionId,
    string CurrentVersionNumber,
    string? Checksum,
    long FileSizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
