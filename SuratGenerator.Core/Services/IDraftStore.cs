using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IDraftStore
{
    Task<DraftInfo> SaveAsync(DraftInfo draft, CancellationToken cancellationToken = default);

    Task<DraftInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DraftInfo>> GetAllAsync(CancellationToken cancellationToken = default);
}
