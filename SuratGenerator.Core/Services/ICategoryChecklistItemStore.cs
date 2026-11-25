using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface ICategoryChecklistItemStore
{
    Task<IReadOnlyList<CategoryChecklistItemInfo>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryChecklistItemInfo>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<CategoryChecklistItemInfo> AddAsync(
        Guid categoryId,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<CategoryChecklistItemInfo> RenameAsync(
        Guid id,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<CategoryChecklistItemInfo> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
