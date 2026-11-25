namespace SuratGenerator.Infrastructure.Data;

public sealed class DraftEntity
{
    public Guid Id { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ValuesJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
