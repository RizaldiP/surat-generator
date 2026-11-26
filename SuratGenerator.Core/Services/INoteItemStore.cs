using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface INoteItemStore
{
    Task<IReadOnlyList<NoteItemInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NoteItemInfo>> GetByNoteAsync(Guid noteId, CancellationToken cancellationToken = default);

    Task<NoteItemInfo> AddAsync(
        Guid noteId,
        string displayName,
        string description,
        CancellationToken cancellationToken = default);

    Task<NoteItemInfo> UpdateAsync(
        Guid id,
        string displayName,
        string description,
        CancellationToken cancellationToken = default);

    Task ReorderAsync(
        Guid noteId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
