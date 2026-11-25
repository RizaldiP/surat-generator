using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IMasterDocumentStore
{
    Task<IReadOnlyList<MasterDocumentInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDocumentInfo>> GetByCategoryAsync(
        Guid categoryId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDocumentVersionInfo>> GetVersionsAsync(
        Guid masterDocumentId,
        CancellationToken cancellationToken = default);

    Task<MasterDocumentInfo> UploadAsync(
        Guid categoryId,
        string sourcePath,
        string displayName,
        bool isRequired,
        CancellationToken cancellationToken = default);

    Task<MasterDocumentInfo> ReplaceVersionAsync(
        Guid masterDocumentId,
        string sourcePath,
        string? displayName = null,
        CancellationToken cancellationToken = default);

    Task<MasterDocumentInfo> UpdateMetadataAsync(
        Guid id,
        string? displayName = null,
        bool? isRequired = null,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default);

    Task<MasterDocumentInfo> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<MasterDocumentInfo> SetArchivedAsync(
        Guid id,
        bool isArchived,
        CancellationToken cancellationToken = default);

    Task<MasterDocumentInfo> RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    Task<string> ResolveOpenPathAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MasterDocumentInfo> EnsureCurrentChecksumAsync(Guid id, CancellationToken cancellationToken = default);

    Task RelocateAsync(string oldRoot, string newRoot, CancellationToken cancellationToken = default);
}
