using System.Security.Cryptography;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Infrastructure.Services;

public sealed class WorkspaceFileService : IWorkspaceFileService
{
    public async Task<string> CopyAsync(
        string sourcePath,
        string destinationRoot,
        string relativePath,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("File sumber tidak ditemukan.", sourcePath);
        }

        var destination = ResolveDestination(destinationRoot, relativePath);
        if (File.Exists(destination) && !overwrite)
        {
            throw new IOException($"File tujuan sudah ada: {destination}");
        }

        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await using var sourceStream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            true);
        await using var destinationStream = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            true);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        return destination;
    }

    public async Task<string> CopyWithBackupAsync(
        string sourcePath,
        string destinationRoot,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var result = await CopyWithBackupDetailedAsync(sourcePath, destinationRoot, relativePath, cancellationToken);
        return result.Destination;
    }

    public async Task<(string Destination, string? BackupPath)> CopyWithBackupDetailedAsync(
        string sourcePath,
        string destinationRoot,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var destination = ResolveDestination(destinationRoot, relativePath);
        string? backupPath = null;
        if (File.Exists(destination))
        {
            var stamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
            backupPath = await EnsureUniquePathAsync(
                destinationRoot,
                $"{relativePath}.bak-{stamp}",
                cancellationToken);
            File.Move(destination, backupPath);
        }

        var copied = await CopyAsync(sourcePath, destinationRoot, relativePath, false, cancellationToken);
        return (copied, backupPath);
    }

    public Task<string> EnsureUniquePathAsync(
        string destinationRoot,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var destination = ResolveDestination(destinationRoot, relativePath);
        if (!File.Exists(destination) && !Directory.Exists(destination))
        {
            return Task.FromResult(destination);
        }

        var directory = Path.GetDirectoryName(destination) ?? string.Empty;
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(directory, $"{nameWithoutExtension} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return Task.FromResult(candidate);
            }
        }
    }

    public Task<string> CreateStagingDirectoryAsync(
        string projectsDirectory,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stagingRoot = GetStagingRoot(projectsDirectory);
        Directory.CreateDirectory(stagingRoot);
        var safePrefix = PathSafety.SanitizeName(prefix, '_', 60, false);
        var stagingPath = Path.Combine(stagingRoot, $"{safePrefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingPath);
        return Task.FromResult(stagingPath);
    }

    public Task FinalizeStagingAsync(
        string stagingPath,
        string finalPath,
        string projectsDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = PathSafety.NormalizeRoot(projectsDirectory);
        var stagingRoot = GetStagingRoot(root);
        var staging = PathSafety.NormalizeRoot(stagingPath);
        var final = Path.GetFullPath(finalPath);

        if (!PathSafety.IsWithin(stagingRoot, staging, false))
        {
            throw new ArgumentException("Staging tidak berada di folder staging yang diizinkan.", nameof(stagingPath));
        }

        if (!PathSafety.IsWithin(root, final, false) || PathSafety.IsWithin(stagingRoot, final, true))
        {
            throw new ArgumentException("Folder akhir tidak valid.", nameof(finalPath));
        }

        if (!Directory.Exists(staging))
        {
            throw new DirectoryNotFoundException("Folder staging tidak ditemukan.");
        }

        if (Directory.Exists(final) || File.Exists(final))
        {
            throw new IOException($"Folder akhir sudah ada: {final}");
        }

        var parent = Path.GetDirectoryName(final);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        Directory.Move(staging, final);
        return Task.CompletedTask;
    }

    public Task CleanupStagingAsync(
        string stagingPath,
        string projectsDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stagingRoot = GetStagingRoot(projectsDirectory);
        var staging = PathSafety.NormalizeRoot(stagingPath);
        if (!PathSafety.IsWithin(stagingRoot, staging, false))
        {
            throw new ArgumentException("Staging tidak berada di folder staging yang diizinkan.", nameof(stagingPath));
        }

        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, true);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkspaceFileMetadata>> ListFilesAsync(
        string root,
        string relativeDirectory,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = ResolvePath(root, relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<WorkspaceFileMetadata>>(Array.Empty<WorkspaceFileMetadata>());
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(directory, "*", searchOption)
            .Select(path => ToMetadata(root, path))
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IReadOnlyList<WorkspaceFileMetadata>>(files);
    }

    public Task<WorkspaceFileMetadata?> GetFileMetadataAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveDestination(root, relativePath);
        return Task.FromResult(File.Exists(path) ? ToMetadata(root, path) : null);
    }

    public Task<bool> FileExistsAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolveDestination(root, relativePath)));
    }

    public Task<string> ResolvePathAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ResolvePath(root, relativePath));
    }

    public Task<string> ToRelativePathAsync(
        string root,
        string absolutePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRoot = PathSafety.NormalizeRoot(root);
        var candidate = Path.GetFullPath(absolutePath);
        if (!PathSafety.IsWithin(normalizedRoot, candidate, false))
        {
            throw new ArgumentException("Path berada di luar root yang diizinkan.", nameof(absolutePath));
        }

        return Task.FromResult(Path.GetRelativePath(normalizedRoot, candidate));
    }

    public Task<long> GetAvailableFreeSpaceAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Path tidak memiliki root drive.", nameof(path));
        }

        return Task.FromResult(new DriveInfo(root).AvailableFreeSpace);
    }

    public Task DeleteFileAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolveDestination(root, relativePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task<string> ComputeChecksumAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("File tidak ditemukan untuk verifikasi checksum.", path);
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            81920,
            true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string ResolvePath(string root, string relativePath) =>
        string.IsNullOrWhiteSpace(relativePath)
            ? PathSafety.NormalizeRoot(root)
            : ResolveDestination(root, relativePath);

    private static WorkspaceFileMetadata ToMetadata(string root, string path)
    {
        var info = new FileInfo(path);
        return new WorkspaceFileMetadata(
            Path.GetRelativePath(PathSafety.NormalizeRoot(root), info.FullName),
            info.Name,
            info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc));
    }

    private static string GetStagingRoot(string projectsDirectory) =>
        Path.Combine(PathSafety.NormalizeRoot(projectsDirectory), ".staging");

    private static string ResolveDestination(string destinationRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Path relatif tidak boleh kosong.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Path relatif harus absolut di dalam root.", nameof(relativePath));
        }

        var root = PathSafety.NormalizeRoot(destinationRoot);
        var segments = relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("Path relatif tidak valid.", nameof(relativePath));
        }

        foreach (var segment in segments)
        {
            if (segment == ".."
                || PathSafety.HasInvalidFileNameChars(segment)
                || PathSafety.HasTrailingDotOrSpace(segment)
                || PathSafety.IsReservedName(segment))
            {
                throw new ArgumentException($"Segmen path tidak valid: {segment}", nameof(relativePath));
            }
        }

        var destination = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!PathSafety.IsWithin(root, destination, false))
        {
            throw new ArgumentException("Path tujuan berada di luar root yang diizinkan.", nameof(relativePath));
        }

        return destination;
    }
}
