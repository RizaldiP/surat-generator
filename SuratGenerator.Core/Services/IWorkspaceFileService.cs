using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface IWorkspaceFileService
{
    Task<string> CopyAsync(
        string sourcePath,
        string destinationRoot,
        string relativePath,
        bool overwrite,
        CancellationToken cancellationToken = default);

    Task<string> CopyWithBackupAsync(
        string sourcePath,
        string destinationRoot,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<string> EnsureUniquePathAsync(
        string destinationRoot,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<string> CreateStagingDirectoryAsync(
        string projectsDirectory,
        string prefix,
        CancellationToken cancellationToken = default);

    Task FinalizeStagingAsync(
        string stagingPath,
        string finalPath,
        string projectsDirectory,
        CancellationToken cancellationToken = default);

    Task CleanupStagingAsync(
        string stagingPath,
        string projectsDirectory,
        CancellationToken cancellationToken = default);

    Task<(string Destination, string? BackupPath)> CopyWithBackupDetailedAsync(
        string sourcePath,
        string destinationRoot,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceFileMetadata>> ListFilesAsync(
        string root,
        string relativeDirectory,
        bool recursive = false,
        CancellationToken cancellationToken = default);

    Task<WorkspaceFileMetadata?> GetFileMetadataAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<bool> FileExistsAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<string> ResolvePathAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<string> ToRelativePathAsync(
        string root,
        string absolutePath,
        CancellationToken cancellationToken = default);

    Task<long> GetAvailableFreeSpaceAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<string> ComputeChecksumAsync(
        string path,
        CancellationToken cancellationToken = default);
}
