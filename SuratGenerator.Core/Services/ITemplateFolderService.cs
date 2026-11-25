using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface ITemplateFolderService
{
    Task<TemplateFolderSettingsInfo> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<TemplateFolderSettingsInfo> SetFolderAsync(
        string folderPath,
        bool includeSubfolders,
        CancellationToken cancellationToken = default);

    Task<TemplateFolderScanSummary> ScanAsync(
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
}
