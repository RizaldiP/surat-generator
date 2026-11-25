using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Infrastructure.Services;

public sealed class WorkspacePathService : IWorkspacePathService
{
    private readonly WorkspaceSettingsStore _settings;
    private readonly string _dataDirectory;

    public WorkspacePathService()
        : this(AppPaths.WorkspaceSettingsPath, AppPaths.DataDirectory)
    {
    }

    public WorkspacePathService(string settingsPath, string dataDirectory)
    {
        _settings = new WorkspaceSettingsStore(settingsPath);
        _dataDirectory = Path.GetFullPath(dataDirectory);
    }

    public async Task<WorkspacePathsInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _settings.GetAsync(cancellationToken);
        if (stored is null)
        {
            return DefaultPaths();
        }

        var master = ValidateTargetDirectory(stored.MasterDocumentsDirectory);
        var projects = ValidateTargetDirectory(stored.ProjectsDirectory);
        var backups = ValidateTargetDirectory(stored.BackupsDirectory);
        return master.IsValid && projects.IsValid && backups.IsValid ? stored : DefaultPaths();
    }

    public async Task<WorkspacePathsInfo> SetAsync(
        string masterDocumentsDirectory,
        string projectsDirectory,
        string backupsDirectory,
        CancellationToken cancellationToken = default)
    {
        var master = ValidateTargetDirectory(masterDocumentsDirectory);
        var projects = ValidateTargetDirectory(projectsDirectory);
        var backups = ValidateTargetDirectory(backupsDirectory);

        if (!master.IsValid)
        {
            throw new ArgumentException(master.ErrorMessage, nameof(masterDocumentsDirectory));
        }

        if (!projects.IsValid)
        {
            throw new ArgumentException(projects.ErrorMessage, nameof(projectsDirectory));
        }

        if (!backups.IsValid)
        {
            throw new ArgumentException(backups.ErrorMessage, nameof(backupsDirectory));
        }

        var masterPath = master.NormalizedPath!;
        var projectsPath = projects.NormalizedPath!;
        var backupsPath = backups.NormalizedPath!;

        if (AreSame(masterPath, projectsPath)
            || AreSame(masterPath, backupsPath)
            || AreSame(projectsPath, backupsPath))
        {
            throw new ArgumentException("Lokasi master, pekerjaan, dan backup harus berbeda.");
        }

        if (PathSafety.IsWithin(masterPath, backupsPath, false)
            || PathSafety.IsWithin(projectsPath, backupsPath, false))
        {
            throw new ArgumentException("Lokasi backup tidak boleh berada di dalam lokasi master atau pekerjaan.");
        }

        var paths = new WorkspacePathsInfo(masterPath, projectsPath, backupsPath);
        await _settings.SetAsync(paths, cancellationToken);
        return await EnsureDirectoriesCoreAsync(paths, cancellationToken);
    }

    public async Task<WorkspacePathsInfo> EnsureDirectoriesAsync(CancellationToken cancellationToken = default)
    {
        var paths = await GetAsync(cancellationToken);
        return await EnsureDirectoriesCoreAsync(paths, cancellationToken);
    }

    public PathValidationResult ValidateTargetDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Failure("Path tidak boleh kosong.");
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return PathValidationResult.Failure("Path mengandung karakter yang tidak valid.");
        }

        if (!Path.IsPathRooted(path))
        {
            return PathValidationResult.Failure("Path harus absolut.");
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathValidationResult.Failure("Path tidak dapat dinormalisasi.");
        }

        if (normalized.Length > 240)
        {
            return PathValidationResult.Failure("Path terlalu panjang.");
        }

        var root = Path.GetPathRoot(normalized) ?? string.Empty;
        var segments = normalized[root.Length..]
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (PathSafety.HasInvalidFileNameChars(segment)
                || PathSafety.HasTrailingDotOrSpace(segment)
                || PathSafety.IsReservedName(segment))
            {
                return PathValidationResult.Failure($"Nama folder tidak valid: {segment}");
            }
        }

        var protectedRoots = new[]
        {
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        foreach (var protectedRoot in protectedRoots)
        {
            if (!string.IsNullOrWhiteSpace(protectedRoot)
                && PathSafety.IsWithin(protectedRoot, normalized, true))
            {
                return PathValidationResult.Failure("Path tidak boleh berada di folder instalasi atau Program Files.");
            }
        }

        return PathValidationResult.Success(PathSafety.NormalizeRoot(normalized));
    }

    public string SanitizeFileName(string value) =>
        PathSafety.SanitizeName(value, '_', 120, false);

    public string BuildProjectFolderName(string shipName, string jobName)
    {
        var ship = PathSafety.SanitizeName(shipName, ' ', 60, true);
        var job = PathSafety.SanitizeName(jobName, ' ', 60, true);
        return $"{ship} - {job}";
    }

    public string GetStagingRoot(string projectsDirectory) =>
        Path.Combine(PathSafety.NormalizeRoot(projectsDirectory), ".staging");

    private WorkspacePathsInfo DefaultPaths() =>
        new(
            Path.Combine(_dataDirectory, "MasterDocuments"),
            Path.Combine(_dataDirectory, "Projects"),
            Path.Combine(_dataDirectory, "Backups"));

    private static bool AreSame(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static async Task<WorkspacePathsInfo> EnsureDirectoriesCoreAsync(
        WorkspacePathsInfo paths,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        Directory.CreateDirectory(paths.MasterDocumentsDirectory);
        Directory.CreateDirectory(paths.ProjectsDirectory);
        Directory.CreateDirectory(paths.BackupsDirectory);
        cancellationToken.ThrowIfCancellationRequested();
        return paths;
    }
}
