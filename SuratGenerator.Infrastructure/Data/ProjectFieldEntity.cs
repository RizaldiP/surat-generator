namespace SuratGenerator.Infrastructure.Data;

public sealed class ProjectFieldEntity
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string FieldType { get; set; } = "Text";

    public string Source { get; set; } = string.Empty;
}
