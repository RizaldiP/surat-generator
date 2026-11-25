using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IBackupRecordStore
{
    Task<BackupRecordInfo> AddAsync(BackupRecordInfo record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupRecordInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<BackupRecordInfo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
