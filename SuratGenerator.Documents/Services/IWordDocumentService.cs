using SuratGenerator.Core.Models;

namespace SuratGenerator.Documents.Services;

public interface IWordDocumentService : IWordPdfRenderer
{
    Task ExportAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        string outputPath,
        GeneratedDocumentKind kind,
        CancellationToken cancellationToken = default);

    Task PrintAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);
}
