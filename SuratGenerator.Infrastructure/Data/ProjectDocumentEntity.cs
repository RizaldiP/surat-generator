namespace SuratGenerator.Infrastructure.Data;

public sealed class ProjectDocumentEntity
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid ProjectCategoryId { get; set; }

    public Guid? MasterDocumentId { get; set; }

    public Guid? MasterVersionId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string VersionSnapshot { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTimeOffset? LastModifiedUtc { get; set; }

    public string? BaseTemplateFilePath { get; set; }

    public string? EditedOutputFilePath { get; set; }

    public DateTimeOffset? LastEditedAt { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
