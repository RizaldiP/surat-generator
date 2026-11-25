using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase9BackupTests
{
    [Fact]
    public async Task FinalizeMenolakTujuanRestoreDiLuarWorkspaceDanMenyimpanError()
    {
        var environment = CreateEnvironment();
        try
        {
            var project = await environment.SeedProjectAsync("KRI Empat", "Hardening");
            var projectFile = Path.Combine(environment.ProjectsRoot, project.FolderName, "Surat.docx");
            await File.WriteAllTextAsync(projectFile, "isi lama");

            var backup = await environment.BackupService.CreateAsync(new BackupRequest(
                environment.BackupsRoot,
                BackupKinds.Manual,
                true,
                Array.Empty<Guid>(),
                null));
            await environment.BackupService.StageRestoreAsync(new RestoreRequest(backup.ArchivePath));

            var marker = ReadMarker(environment.MarkerPath);
            marker.Entries[0].DestinationPath = Path.Combine(environment.Directory, "Outside", "evil.txt");
            WriteMarker(environment.MarkerPath, marker);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => environment.BackupService.FinalizePendingRestoreAsync());

            Assert.False(File.Exists(environment.MarkerPath));
            Assert.False(File.Exists(marker.Entries[0].DestinationPath));
            Assert.Equal("isi lama", await File.ReadAllTextAsync(projectFile));
            Assert.Contains("Tujuan restore tidak valid", await environment.BackupService.GetLastRestoreErrorAsync());
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task FinalizeMenolakFileStagingYangDimanipulasiDanMenyimpanError()
    {
        var environment = CreateEnvironment();
        try
        {
            var project = await environment.SeedProjectAsync("KRI Lima", "Staging");
            var projectFile = Path.Combine(environment.ProjectsRoot, project.FolderName, "Surat.docx");
            await File.WriteAllTextAsync(projectFile, "isi lama");

            var backup = await environment.BackupService.CreateAsync(new BackupRequest(
                environment.BackupsRoot,
                BackupKinds.Manual,
                true,
                Array.Empty<Guid>(),
                null));
            await environment.BackupService.StageRestoreAsync(new RestoreRequest(backup.ArchivePath));

            var marker = ReadMarker(environment.MarkerPath);
            var staged = marker.Entries.First(entry => entry.EntryPath.StartsWith("projects/", StringComparison.Ordinal));
            await File.WriteAllTextAsync(staged.StagedPath, "isi staging yang dimanipulasi");

            await Assert.ThrowsAsync<InvalidDataException>(
                () => environment.BackupService.FinalizePendingRestoreAsync());

            Assert.False(File.Exists(environment.MarkerPath));
            Assert.Equal("isi lama", await File.ReadAllTextAsync(projectFile));
            Assert.Contains("file restore tidak sesuai", await environment.BackupService.GetLastRestoreErrorAsync());
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task BackupDitolakSaatLockDipegangProsesLain()
    {
        var environment = CreateEnvironment();
        try
        {
            Directory.CreateDirectory(environment.PendingRoot);
            var lockPath = Path.Combine(environment.PendingRoot, "backup.lock");
            await using var lockStream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            await Assert.ThrowsAnyAsync<IOException>(() => environment.BackupService.CreateAsync(
                new BackupRequest(
                    environment.BackupsRoot,
                    BackupKinds.Manual,
                    true,
                    Array.Empty<Guid>(),
                    null)));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task BackupMenolakFolderTujuanDiDalamFolderSumber()
    {
        var environment = CreateEnvironment();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => environment.BackupService.CreateAsync(
                new BackupRequest(
                    environment.ProjectsRoot,
                    BackupKinds.Manual,
                    true,
                    Array.Empty<Guid>(),
                    null)));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task BackupRecordStoreMengurutkanCatatanPadaSqlite()
    {
        var environment = CreateEnvironment();
        try
        {
            using var store = new BackupRecordStore(environment.Options, NullAppLogger.Instance);
            await store.AddAsync(new BackupRecordInfo(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-2),
                "lama.zip",
                BackupKinds.Manual,
                BackupStatuses.Berhasil,
                true,
                0,
                0,
                null));
            await store.AddAsync(new BackupRecordInfo(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "baru.zip",
                BackupKinds.Manual,
                BackupStatuses.Berhasil,
                true,
                0,
                0,
                null));

            var records = await store.GetAllAsync();

            Assert.Equal("baru.zip", records[0].Location);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task AutoBackupMembuatUlangArsipYangSudahHilang()
    {
        var environment = CreateEnvironment();
        try
        {
            var first = await environment.BackupService.EnsureDailyBackupAsync();
            Assert.NotNull(first);

            File.Delete(first.ArchivePath);

            var second = await environment.BackupService.EnsureDailyBackupAsync();
            Assert.NotNull(second);
            Assert.NotEqual(first.ArchivePath, second.ArchivePath);
            Assert.True(File.Exists(second.ArchivePath));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    private static TestMarker ReadMarker(string path) =>
        JsonSerializer.Deserialize<TestMarker>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException("Marker restore tidak terbaca.");

    private static void WriteMarker(string path, TestMarker marker) =>
        File.WriteAllText(path, JsonSerializer.Serialize(marker, JsonOptions));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static void Cleanup(TestEnvironment environment)
    {
        environment.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(environment.Directory))
        {
            Directory.Delete(environment.Directory, true);
        }
    }

    private static TestEnvironment CreateEnvironment()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase9Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "test.db");
        var templatesRoot = Path.Combine(directory, "Templates");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var projectsRoot = Path.Combine(directory, "Projects");
        var backupsRoot = Path.Combine(directory, "Backups");
        var pendingRoot = Path.Combine(directory, "PendingRestore");
        Directory.CreateDirectory(templatesRoot);
        Directory.CreateDirectory(masterRoot);
        Directory.CreateDirectory(projectsRoot);
        Directory.CreateDirectory(backupsRoot);

        var options = new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        using (var initializerStore = new BackupRecordStore(options, NullAppLogger.Instance))
        {
            _ = initializerStore;
        }

        var configPath = Path.Combine(directory, "workspace-paths.json");
        var paths = new BackupPaths(
            databasePath,
            templatesRoot,
            new[] { configPath },
            pendingRoot,
            Path.Combine(pendingRoot, "pending-restore.json"));
        var pathService = new TestWorkspacePathService(masterRoot, projectsRoot);
        return new TestEnvironment(
            directory,
            databasePath,
            masterRoot,
            projectsRoot,
            backupsRoot,
            pendingRoot,
            configPath,
            options,
            pathService,
            paths);
    }

    private sealed class TestEnvironment : IDisposable
    {
        private readonly TestBackupRecordStore _recordStore = new();

        public TestEnvironment(
            string directory,
            string databasePath,
            string masterRoot,
            string projectsRoot,
            string backupsRoot,
            string pendingRoot,
            string configPath,
            DbContextOptions<SuratGeneratorDbContext> options,
            IWorkspacePathService pathService,
            BackupPaths backupPaths)
        {
            Directory = directory;
            DatabasePath = databasePath;
            MasterRoot = masterRoot;
            ProjectsRoot = projectsRoot;
            BackupsRoot = backupsRoot;
            PendingRoot = pendingRoot;
            ConfigPath = configPath;
            Options = options;
            PathService = pathService;
            BackupPaths = backupPaths;
            BackupService = new BackupService(
                options,
                pathService,
                NullAppLogger.Instance,
                () => _recordStore,
                backupPaths);
        }

        public string Directory { get; }

        public string DatabasePath { get; }

        public string MasterRoot { get; }

        public string ProjectsRoot { get; }

        public string BackupsRoot { get; }

        public string PendingRoot { get; }

        public string ConfigPath { get; }

        public string MarkerPath => BackupPaths.PendingRestoreMarkerPath;

        public DbContextOptions<SuratGeneratorDbContext> Options { get; }

        public IWorkspacePathService PathService { get; }

        public BackupPaths BackupPaths { get; }

        public IBackupService BackupService { get; }

        public async Task<(Guid Id, string FolderName)> SeedProjectAsync(string shipName, string jobName)
        {
            var id = Guid.NewGuid();
            var folderName = string.Join(' ', $"{shipName} {jobName}".Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var rootPath = Path.Combine(ProjectsRoot, folderName);
            System.IO.Directory.CreateDirectory(rootPath);
            var now = DateTimeOffset.UtcNow;
            using var db = new SuratGeneratorDbContext(Options);
            db.Projects.Add(new ProjectEntity
            {
                Id = id,
                ShipName = shipName,
                JobName = jobName,
                RootPath = rootPath,
                Status = ProjectStatuses.Active,
                StructureVersion = "v3.1",
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
            return (id, folderName);
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestMarker
    {
        public string ArchivePath { get; set; } = string.Empty;

        public string StagedDirectory { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public string? PreRestoreBackupPath { get; set; }

        public List<TestMarkerEntry> Entries { get; set; } = new();
    }

    private sealed class TestMarkerEntry
    {
        public string EntryPath { get; set; } = string.Empty;

        public string StagedPath { get; set; } = string.Empty;

        public string DestinationPath { get; set; } = string.Empty;

        public bool? HadExistingDestination { get; set; }

        public string Sha256 { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }

    private sealed class TestBackupRecordStore : IBackupRecordStore
    {
        private readonly List<BackupRecordInfo> _records = new();

        public Task<BackupRecordInfo> AddAsync(
            BackupRecordInfo record,
            CancellationToken cancellationToken = default)
        {
            _records.Add(record);
            return Task.FromResult(record);
        }

        public Task<IReadOnlyList<BackupRecordInfo>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BackupRecordInfo>>(
                _records.OrderByDescending(record => record.CreatedAt).ToArray());

        public Task<BackupRecordInfo?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_records.FirstOrDefault(record => record.Id == id));
    }

    private sealed class TestWorkspacePathService : IWorkspacePathService
    {
        private WorkspacePathsInfo _paths;

        public TestWorkspacePathService(string masterRoot, string projectsRoot)
        {
            _paths = new WorkspacePathsInfo(
                masterRoot,
                projectsRoot,
                Path.Combine(Path.GetDirectoryName(projectsRoot)!, "Backups"));
        }

        public Task<WorkspacePathsInfo> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_paths);

        public Task<WorkspacePathsInfo> SetAsync(
            string masterDocumentsDirectory,
            string projectsDirectory,
            string backupsDirectory,
            CancellationToken cancellationToken = default)
        {
            _paths = new WorkspacePathsInfo(masterDocumentsDirectory, projectsDirectory, backupsDirectory);
            return EnsureDirectoriesAsync(cancellationToken);
        }

        public Task<WorkspacePathsInfo> EnsureDirectoriesAsync(CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(_paths.MasterDocumentsDirectory);
            Directory.CreateDirectory(_paths.ProjectsDirectory);
            Directory.CreateDirectory(_paths.BackupsDirectory);
            return Task.FromResult(_paths);
        }

        public PathValidationResult ValidateTargetDirectory(string path) =>
            PathValidationResult.Success(Path.GetFullPath(path));

        public string SanitizeFileName(string value) => value;

        public string BuildProjectFolderName(string shipName, string jobName) => $"{shipName} {jobName}";

        public string GetStagingRoot(string projectsDirectory) => Path.Combine(projectsDirectory, ".staging");
    }
}
