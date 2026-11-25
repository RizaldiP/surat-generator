using SuratGenerator.Documents.Models;

namespace SuratGenerator.Documents.Services;

public interface ITemplateReader
{
    Task<TemplateReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
