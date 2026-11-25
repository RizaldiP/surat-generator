namespace SuratGenerator.Infrastructure.Data;

public sealed class TemplateEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SourceFileName { get; set; } = string.Empty;

    public string StoredFilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public string Category { get; set; } = "Umum";

    public bool IsFavorite { get; set; }

    public string Version { get; set; } = "1.0";

    public string? SourceFilePath { get; set; }

    public string? SourceChecksum { get; set; }

    public DateTimeOffset? SourceModifiedAt { get; set; }

    public List<TemplateFieldEntity> Fields { get; set; } = new();
}
