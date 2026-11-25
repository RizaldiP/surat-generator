using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IDocumentFieldValueStore
{
    Task<DocumentFieldEditInfo> EnsureEditSessionAsync(
        Guid projectDocumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaceholderMappingInfo>> GetMappingsAsync(
        CancellationToken cancellationToken = default);

    Task<string> PrepareOutputAsync(
        Guid projectDocumentId,
        string outputPath,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        DocumentFieldEditSaveRequest request,
        CancellationToken cancellationToken = default);

    Task PromoteToProjectFieldsAsync(
        Guid projectDocumentId,
        IReadOnlyList<ProjectFieldSaveInfo> fields,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUnfilledRequiredPlaceholdersAsync(
        Guid projectDocumentId,
        CancellationToken cancellationToken = default);
}
