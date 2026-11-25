namespace SuratGenerator.Infrastructure.Data;

public sealed class MasterDocumentVersionEntity
{
    public Guid Id { get; set; }

    public Guid MasterDocumentId { get; set; }

    public string VersionNumber { get; set; } = string.Empty;

    public string StoredFilePath { get; set; } = string.Empty;

    public string? Checksum { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
