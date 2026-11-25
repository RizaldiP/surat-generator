namespace SuratGenerator.Core.Models;

public sealed record GeneratedDocumentInfo(
    Guid Id,
    Guid TemplateId,
    string TemplateName,
    string DocumentName,
    GeneratedDocumentKind Kind,
    string FilePath,
    long FileSizeBytes,
    DateTimeOffset GeneratedAt)
{
    public string GeneratedAtText => GeneratedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm");

    public string FileSizeText => FileSizeBytes <= 0
        ? "-"
        : $"{FileSizeBytes / 1024.0:F0} KB";

    public string FormatLabel => Kind switch
    {
        GeneratedDocumentKind.Docx => "Word",
        GeneratedDocumentKind.Pdf => "PDF",
        GeneratedDocumentKind.Print => "Cetak",
        _ => Kind.ToString()
    };

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
}
