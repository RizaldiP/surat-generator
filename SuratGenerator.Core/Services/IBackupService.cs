using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IBackupService
{
    Task<BackupPreview> PreviewAsync(string archivePath, CancellationToken cancellationToken = default);

    Task<BackupResult> CreateAsync(
        BackupRequest request,
        IProgress<BackupProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<RestoreResult> StageRestoreAsync(
        RestoreRequest request,
        IProgress<BackupProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<BackupResult?> EnsureDailyBackupAsync(CancellationToken cancellationToken = default);

    Task<bool> FinalizePendingRestoreAsync(CancellationToken cancellationToken = default);

    Task<bool> HasPendingRestoreAsync(CancellationToken cancellationToken = default);

    Task<string?> GetLastRestoreErrorAsync(CancellationToken cancellationToken = default);
}
