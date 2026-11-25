using SuratGenerator.Documents.Models;

namespace SuratGenerator.Documents.Services;

public interface ILegacyTemplateConverter
{
    Task<TemplateConversionResult> ConvertToDocxAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);
}
