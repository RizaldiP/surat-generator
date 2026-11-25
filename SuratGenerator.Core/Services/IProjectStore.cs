using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IProjectStore
{
    Task<ProjectCreationResult> CreateAsync(
        ProjectCreationRequest request,
        IProgress<ProjectCreationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ProjectDuplicateInfo?> FindDuplicateAsync(
        string shipName,
        string jobName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectInfo>> GetAllAsync(
        bool includeMissing = false,
        CancellationToken cancellationToken = default);

    Task<ProjectDetailInfo?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProjectUploadResult> UploadDocumentsAsync(
        Guid projectId,
        Guid projectCategoryId,
        IReadOnlyList<string> sourcePaths,
        Func<ProjectFileCollisionInfo, CancellationToken, Task<ProjectUploadCollisionPolicy>> resolveCollision,
        IProgress<ProjectCreationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ProjectRefreshResult> RefreshFilesystemAsync(
        Guid projectId,
        IProgress<ProjectCreationProgressInfo>? progress = null,
        bool pullMissingMasters = false,
        CancellationToken cancellationToken = default);

    Task<string> ResolveProjectPathAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<string> ResolveCategoryPathAsync(Guid projectCategoryId, CancellationToken cancellationToken = default);

    Task<string> ResolveDocumentPathAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<ProjectDocumentInfo> SetDocumentStatusAsync(
        Guid documentId,
        string status,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}
