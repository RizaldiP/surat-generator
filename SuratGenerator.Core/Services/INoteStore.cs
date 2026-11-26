using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface INoteStore
{
    Task<IReadOnlyList<NoteInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<NoteInfo> AddAsync(string title, CancellationToken cancellationToken = default);

    Task<NoteInfo> RenameAsync(Guid id, string title, CancellationToken cancellationToken = default);

    Task ReorderAsync(IReadOnlyList<Guid> orderedIds, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
