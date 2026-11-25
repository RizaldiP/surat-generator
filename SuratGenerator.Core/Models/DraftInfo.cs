namespace SuratGenerator.Core.Models;

public sealed record DraftInfo(
    Guid Id,
    Guid TemplateId,
    string TemplateName,
    string Name,
    IReadOnlyDictionary<string, string> Values,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public string UpdatedAtText => UpdatedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm");
}
