namespace SuratGenerator.Infrastructure.Data;

public sealed class TemplateFieldEntity
{
    public long Id { get; set; }

    public Guid TemplateId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int Position { get; set; }

    public string Kind { get; set; } = string.Empty;

    public TemplateEntity Template { get; set; } = null!;
}
