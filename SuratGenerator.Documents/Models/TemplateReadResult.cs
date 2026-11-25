using SuratGenerator.Core.Models;

namespace SuratGenerator.Documents.Models;

public sealed record TemplateReadResult(
    IReadOnlyList<TemplateField> Fields,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public static TemplateReadResult Success(IReadOnlyList<TemplateField> fields) => new(fields, null);

    public static TemplateReadResult Failure(string error) => new(Array.Empty<TemplateField>(), error);
}
