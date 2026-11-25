namespace SuratGenerator.Core.Models;

public sealed record TemplateField(
    string Key,
    string Label,
    int Position,
    TemplateFieldKind Kind);
