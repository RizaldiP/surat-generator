namespace SuratGenerator.Infrastructure.Data;

public sealed class MasterDocumentEntity
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string SourceFileName { get; set; } = string.Empty;

    public string StoredFilePath { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsArchived { get; set; }

    public Guid? CurrentVersionId { get; set; }

    public string CurrentVersionNumber { get; set; } = "1.0";

    public string? Checksum { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
