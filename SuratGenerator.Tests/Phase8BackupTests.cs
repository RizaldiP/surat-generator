using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase8BackupTests
{
    [Fact]
    public async Task BackupMembuatArsipValidDanTerverifikasi()
    {
        var environment = CreateEnvironment();
        try
        {
            var project = await environment.SeedProjectAsync("KRI Satu", "Backup");
            await File.WriteAllTextAsync(
                Path.Combine(environment.ProjectsRoot, project.FolderName, "Surat.docx"),
                "isi proyek");
            await File.WriteAllTextAsync(Path.Combine(environment.MasterRoot, "SK.pdf"), "master");
            await File.WriteAllTextAsync(environment.ConfigPath, "{}");

            var result = await environment.BackupService.CreateAsync(new BackupRequest(
                environment.BackupsRoot,
                BackupKinds.Manual,
                true,
                Array.Empty<Guid>(),
                "Uji backup"));

            Assert.True(File.Exists(result.ArchivePath));
            Assert.True(result.Record.IsVerified);
            Assert.Equal(BackupStatuses.Berhasil, result.Record.Status);
            Assert.Contains(result.Manifest.Entries, entry => entry.Path == "database/surat-generator.db");
            Assert.Contains(result.Manifest.Entries, entry => entry.Path == "config/workspace-paths.json");
            Assert.Contains(result.Manifest.Entries, entry => entry.Path == "master/SK.pdf");
            Assert.Contains(result.Manifest.Entries, entry => entry.Path.StartsWith("projects/", StringComparison.Ordinal));

            var preview = await environment.BackupService.PreviewAsync(result.ArchivePath);
            Assert.True(preview.CanRestore);
            Assert.Contains(preview.Warnings, warning => warning.Contains("Token Google Drive", StringComparison.Ordinal));
            Assert.Contains(preview.Conflicts, conflict => conflict.Contains("Database aktif", StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task PreviewMenolakArsipDenganPathTidakAman()
    {
        var environment = CreateEnvironment();
        try
        {
            var archivePath = Path.Combine(environment.BackupsRoot, "unsafe.zip");
            Directory.CreateDirectory(environment.BackupsRoot);
            var digest = Convert.ToHexString(SHA256.HashData("isi"u8.ToArray()));
            var manifest = new BackupManifest(
                BackupFormats.Current,
                DateTimeOffset.UtcNow,
                BackupKinds.Manual,
                new[] { new BackupEntry("../evil.txt", 3, digest) },
                Array.Empty<BackupProjectInfo>());

            await using (var stream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                await using (var manifestStream = zip.CreateEntry("manifest.json").Open())
                {
                    await System.Text.Json.JsonSerializer.SerializeAsync(manifestStream, manifest);
                }

                await using var entryStream = zip.CreateEntry("../evil.txt").Open();
                await entryStream.WriteAsync("isi"u8.ToArray());
            }

            await Assert.ThrowsAsync<InvalidDataException>(
                () => environment.BackupService.PreviewAsync(archivePath));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RestoreStageDanFinalizeMenimpaDataSetelahRestart()
    {
        var environment = CreateEnvironment();
        try
        {
            var project = await environment.SeedProjectAsync("KRI Dua", "Restore");
            var relative = Path.Combine(project.FolderName, "Surat.docx");
            var projectFile = Path.Combine(environment.ProjectsRoot, relative);
            await File.WriteAllTextAsync(projectFile, "isi lama");

            var backup = await environment.BackupService.CreateAsync(new BackupRequest(
                environment.BackupsRoot,
                BackupKinds.Manual,
                true,
                Array.Empty<Guid>(),
                null));

            await File.WriteAllTextAsync(projectFile, "isi baru");
            var result = await environment.BackupService.StageRestoreAsync(new RestoreRequest(backup.ArchivePath));

            Assert.True(result.RequiresRestart);
            Assert.True(File.Exists(environment.MarkerPath));
            Assert.Equal("isi baru", await File.ReadAllTextAsync(projectFile));
            Assert.True(File.Exists(result.PreRestoreBackupPath!));

            var finalized = await environment.BackupService.FinalizePendingRestoreAsync();

            Assert.True(finalized);
            Assert.False(File.Exists(environment.MarkerPath));
            Assert.Equal("isi lama", await File.ReadAllTextAsync(projectFile));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RestoreHanyaProyekTerpilih()
    {
        var environment = CreateEnvironment();
        try
        {
            var first = await environment.SeedProjectAsync("KRI Satu", "Pilih");
            var second = await environment.SeedProjectAsync("KRI Dua", "Pilih");
            var firstFile = Path.Combine(environment.ProjectsRoot, first.FolderName, "Surat.docx");
            var secondFile = Path.Combine(environment.ProjectsRoot, second.FolderName, "Surat.docx");
            await File.WriteAllTextAsync(firstFile, "awal satu");
            await File.WriteAllTextAsync(secondFile, "awal dua");

            var backup = await environment.BackupService.CreateAsync(new BackupRequest(
                environment.BackupsRoot,
                BackupKinds.Manual,
                true,
                Array.Empty<Guid>(),
                null));

            await File.WriteAllTextAsync(firstFile, "ubah satu");
            await File.WriteAllTextAsync(secondFile, "ubah dua");

            await environment.BackupService.StageRestoreAsync(new RestoreRequest(
                backup.ArchivePath,
                false,
                new[] { first.Id },
                IncludeDatabase: false));
            await environment.BackupService.FinalizePendingRestoreAsync();

            Assert.Equal("awal satu", await File.ReadAllTextAsync(firstFile));
            Assert.Equal("ubah dua", await File.ReadAllTextAsync(secondFile));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task StageRestoreMenolakArsipYangDimanipulasi()
    {
        var environment = CreateEnvironment();
        try
        {
            await environment.SeedProjectAsync("KRI Tiga", "Integritas");
            var backup = await environment.BackupService.CreateAsync(new BackupRequest(
                environment.BackupsRoot,
                BackupKinds.Manual,
                true,
                Array.Empty<Guid>(),
                null));

            await using (var stream = new FileStream(backup.ArchivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Update))
            {
                zip.GetEntry("database/surat-generator.db")!.Delete();
                await using var tampered = zip.CreateEntry("database/surat-generator.db").Open();
                await tampered.WriteAsync("rusak"u8.ToArray());
            }

            await Assert.ThrowsAsync<InvalidDataException>(
                () => environment.BackupService.StageRestoreAsync(new RestoreRequest(backup.ArchivePath)));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task AutoBackupHarianHanyaBuatSatuBackupValid()
    {
        var environment = CreateEnvironment();
        try
        {
            var first = await environment.BackupService.EnsureDailyBackupAsync();
            var second = await environment.BackupService.EnsureDailyBackupAsync();

            Assert.NotNull(first);
            Assert.Null(second);
        }
        finally
        {
            Cleanup(environment);
        }
    }

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
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase8Tests", Guid.NewGuid().ToString("N"));
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
        var environment = new TestEnvironment(
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
        return environment;
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
            var folderName = PathSafetyForTests.Sanitize($"{shipName} {jobName}");
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

    private static class PathSafetyForTests
    {
        public static string Sanitize(string value) => string.Join(
            ' ',
            value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
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
