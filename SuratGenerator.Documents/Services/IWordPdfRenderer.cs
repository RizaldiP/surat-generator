namespace SuratGenerator.Documents.Services;

public interface IWordPdfRenderer
{
    Task<string> RenderAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);

    void Cleanup(string pdfPath);
}
