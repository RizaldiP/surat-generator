namespace SuratGenerator.Documents.Models;

public sealed record TemplateConversionResult(
    string? DocxPath,
    string? Error)
{
    public bool IsSuccess => DocxPath is not null && Error is null;

    public static TemplateConversionResult Success(string docxPath) => new(docxPath, null);

    public static TemplateConversionResult Failure(string error) => new(null, error);
}
