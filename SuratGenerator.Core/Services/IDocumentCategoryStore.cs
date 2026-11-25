using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IDocumentCategoryStore
{
    Task<IReadOnlyList<DocumentCategoryInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MasterStructureSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<DocumentCategoryInfo> AddAsync(string name, CancellationToken cancellationToken = default);

    Task<DocumentCategoryInfo> RenameAsync(Guid id, string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReorderAsync(IReadOnlyList<Guid> orderedIds, CancellationToken cancellationToken = default);

    Task<DocumentCategoryInfo> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
