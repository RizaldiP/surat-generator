using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IWorkspacePathService
{
    Task<WorkspacePathsInfo> GetAsync(CancellationToken cancellationToken = default);

    Task<WorkspacePathsInfo> SetAsync(
        string masterDocumentsDirectory,
        string projectsDirectory,
        string backupsDirectory,
        CancellationToken cancellationToken = default);

    Task<WorkspacePathsInfo> EnsureDirectoriesAsync(CancellationToken cancellationToken = default);

    PathValidationResult ValidateTargetDirectory(string path);

    string SanitizeFileName(string value);

    string BuildProjectFolderName(string shipName, string jobName);

    string GetStagingRoot(string projectsDirectory);
}
