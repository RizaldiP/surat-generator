namespace SuratGenerator.Core.Models;

public sealed record MasterDocumentVersionInfo(
    Guid Id,
    Guid MasterDocumentId,
    string VersionNumber,
    string StoredFilePath,
    string? Checksum,
    long FileSizeBytes,
    DateTimeOffset CreatedAt);
