namespace SuratGenerator.Infrastructure.Data;

public sealed class DocumentFieldValueEntity
{
    public Guid Id { get; set; }

    public Guid ProjectDocumentId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool IsOverride { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
