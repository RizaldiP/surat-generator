namespace SuratGenerator.Core.Services;

using SuratGenerator.Core.Models;

public interface IGoogleDriveService
{
    Task<DriveSettingsInfo> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<DriveSettingsInfo> SetFolderUrlAsync(string folderUrl, CancellationToken cancellationToken = default);

    Task<DriveSettingsInfo> ValidateFolderAsync(string folderUrl, CancellationToken cancellationToken = default);

    Task<DriveSettingsInfo> SignInAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<SyncSummaryInfo> RefreshTemplatesAsync(
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<SyncSummaryInfo> UploadTemplateAsync(
        Guid templateId,
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    Task<SyncSummaryInfo> UploadAllTemplatesAsync(
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
}
