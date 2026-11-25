using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IGeneratedDocumentStore
{
    Task RecordAsync(GeneratedDocumentInfo document, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedDocumentInfo>> GetAllAsync(CancellationToken cancellationToken = default);
}
