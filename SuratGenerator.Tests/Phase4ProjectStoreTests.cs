using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase4ProjectStoreTests
{
    [Fact]
    public async Task CreateAsyncMenyalinMasterDanMenyimpanDetail()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var projectsRoot = Path.Combine(directory, "Projects");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var fileService = new WorkspaceFileService();
            var pathService = new TestWorkspacePathService(masterRoot, projectsRoot);
            using var masters = new MasterDocumentStore(options, pathService, fileService);
            var snapshot = await categories.GetSnapshotAsync();
            var firstCategory = snapshot.Categories[0];
            var secondCategory = snapshot.Categories[1];

            var activeSource = Path.Combine(directory, "panduan.docx");
            await File.WriteAllTextAsync(activeSource, "isi panduan aktif");
            var activeMaster = await masters.UploadAsync(firstCategory.Id, activeSource, "Panduan Aktif", true);

            var archivedSource = Path.Combine(directory, "arsip.docx");
            await File.WriteAllTextAsync(archivedSource, "isi panduan arsip");
            var archivedMaster = await masters.UploadAsync(secondCategory.Id, archivedSource, "Panduan Arsip", true);
            await masters.SetArchivedAsync(archivedMaster.Id, true);

            using var store = new ProjectStore(options, pathService, fileService, categories, masters);
            var reports = new List<ProjectCreationProgressInfo>();
            var request = new ProjectCreationRequest(
                "KRI TBT - 516",
                "Perbaikan Sistem Kelistrikan",
                new[]
                {
                    new ProjectFieldRequest("contract_number", "SPK-001", "Text"),
                    new ProjectFieldRequest("contract_date", "2026-11-24", "Date")
                });

            var result = await store.CreateAsync(request, new SynchronousProgress<ProjectCreationProgressInfo>(reports.Add));

            Assert.False(result.IsDuplicate);
            Assert.Null(result.ExistingProjectId);
            Assert.NotNull(result.Project);
            Assert.Equal("KRI TBT - 516", result.Project.ShipName);
            Assert.Equal(snapshot.Version, result.Project.StructureVersion);
            Assert.True(Directory.Exists(result.RootPath));
            Assert.True(File.Exists(Path.Combine(result.RootPath, $"01. {firstCategory.Name}", "panduan.docx")));
            Assert.Equal(100, reports[^1].Percent);

            var detail = await store.GetDetailAsync(result.Project.Id);
            Assert.NotNull(detail);
            Assert.Equal(9, detail.Categories.Count);
            Assert.Equal($"01. {firstCategory.Name}", detail.Categories[0].Name);
            Assert.Contains(detail.Documents, document =>
                document.Status == DocumentStatuses.Incomplete
                && document.DisplayName == "Panduan Aktif"
                && document.FilePath.Contains(firstCategory.Name, StringComparison.Ordinal)
                && document.VersionSnapshot.Contains(activeMaster.Checksum![..12], StringComparison.Ordinal));
            Assert.Contains(detail.Documents, document =>
                document.Status == DocumentStatuses.NotAvailable
                && document.DisplayName == "Panduan Arsip");
            Assert.Contains(detail.Fields, field => field.Key == ProfileFieldKeys.ShipName && field.Value == "KRI TBT - 516");
            Assert.Contains(detail.Fields, field => field.Key == ProfileFieldKeys.JobName);
            Assert.Contains(detail.Fields, field => field.Key == "contract_number" && field.Value == "SPK-001");
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task CreateAsyncMendeteksiDuplikatDatabaseDanFolder()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var projectsRoot = Path.Combine(directory, "Projects");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var fileService = new WorkspaceFileService();
            var pathService = new TestWorkspacePathService(masterRoot, projectsRoot);
            using var masters = new MasterDocumentStore(options, pathService, fileService);
            using var store = new ProjectStore(options, pathService, fileService, categories, masters);
            var request = new ProjectCreationRequest("KRI A", "Pekerjaan A", Array.Empty<ProjectFieldRequest>());

            var first = await store.CreateAsync(request);
            Assert.NotNull(first.Project);

            var duplicate = await store.CreateAsync(new ProjectCreationRequest("kri a", "pekerjaan a", Array.Empty<ProjectFieldRequest>()));
            Assert.True(duplicate.IsDuplicate);
            Assert.Equal(first.Project.Id, duplicate.ExistingProjectId);

            var orphanPath = Path.Combine(projectsRoot, pathService.BuildProjectFolderName("KRI B", "Pekerjaan B"));
            Directory.CreateDirectory(orphanPath);
            var filesystemDuplicate = await store.FindDuplicateAsync("KRI B", "Pekerjaan B");
            Assert.NotNull(filesystemDuplicate);
            Assert.Null(filesystemDuplicate.ProjectId);
            Assert.Equal(orphanPath, filesystemDuplicate.RootPath);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task CreateAsyncMengizinkanNamaKapalSamaDenganPekerjaanBerbeda()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var projectsRoot = Path.Combine(directory, "Projects");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var fileService = new WorkspaceFileService();
            var pathService = new TestWorkspacePathService(masterRoot, projectsRoot);
            using var masters = new MasterDocumentStore(options, pathService, fileService);
            using var store = new ProjectStore(options, pathService, fileService, categories, masters);

            var first = await store.CreateAsync(new ProjectCreationRequest("KRI A", "Pekerjaan A", Array.Empty<ProjectFieldRequest>()));
            var second = await store.CreateAsync(new ProjectCreationRequest("KRI A", "Pekerjaan B", Array.Empty<ProjectFieldRequest>()));

            Assert.NotNull(first.Project);
            Assert.NotNull(second.Project);
            Assert.False(second.IsDuplicate);
            Assert.NotEqual(first.Project.Id, second.Project.Id);

            var duplicate = await store.CreateAsync(new ProjectCreationRequest("kri a", "pekerjaan a", Array.Empty<ProjectFieldRequest>()));
            Assert.True(duplicate.IsDuplicate);
            Assert.Equal(first.Project.Id, duplicate.ExistingProjectId);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task CreateAsyncChecksumTidakCocokMembatalkanStagingDanDatabase()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var projectsRoot = Path.Combine(directory, "Projects");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var realFileService = new WorkspaceFileService();
            var pathService = new TestWorkspacePathService(masterRoot, projectsRoot);
            using var masters = new MasterDocumentStore(options, pathService, realFileService);
            var category = (await categories.GetSnapshotAsync()).Categories[0];
            var sourcePath = Path.Combine(directory, "panduan.docx");
            await File.WriteAllTextAsync(sourcePath, "isi panduan");
            await masters.UploadAsync(category.Id, sourcePath, "Panduan", true);

            var failingFileService = new ChecksumFailureFileService(realFileService);
            using var store = new ProjectStore(options, pathService, failingFileService, categories, masters);
            var request = new ProjectCreationRequest("KRI C", "Pekerjaan C", Array.Empty<ProjectFieldRequest>());

            await Assert.ThrowsAsync<IOException>(() => store.CreateAsync(request));

            Assert.Empty(await store.GetAllAsync());
            var finalPath = Path.Combine(projectsRoot, pathService.BuildProjectFolderName("KRI C", "Pekerjaan C"));
            Assert.False(Directory.Exists(finalPath));
            var stagingRoot = pathService.GetStagingRoot(projectsRoot);
            Assert.Empty(Directory.Exists(stagingRoot) ? Directory.GetDirectories(stagingRoot) : Array.Empty<string>());
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase4Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<SuratGeneratorDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _action;

        public SynchronousProgress(Action<T> action)
        {
            _action = action;
        }

        public void Report(T value) => _action(value);
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

    private sealed class ChecksumFailureFileService : IWorkspaceFileService
    {
        private readonly IWorkspaceFileService _inner;

        public ChecksumFailureFileService(IWorkspaceFileService inner)
        {
            _inner = inner;
        }

        public Task<string> CopyAsync(
            string sourcePath,
            string destinationRoot,
            string relativePath,
            bool overwrite,
            CancellationToken cancellationToken = default) =>
            _inner.CopyAsync(sourcePath, destinationRoot, relativePath, overwrite, cancellationToken);

        public Task<string> CopyWithBackupAsync(
            string sourcePath,
            string destinationRoot,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _inner.CopyWithBackupAsync(sourcePath, destinationRoot, relativePath, cancellationToken);

        public Task<string> EnsureUniquePathAsync(
            string destinationRoot,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _inner.EnsureUniquePathAsync(destinationRoot, relativePath, cancellationToken);

        public Task<string> CreateStagingDirectoryAsync(
            string projectsDirectory,
            string prefix,
            CancellationToken cancellationToken = default) =>
            _inner.CreateStagingDirectoryAsync(projectsDirectory, prefix, cancellationToken);

        public Task FinalizeStagingAsync(
            string stagingPath,
            string finalPath,
            string projectsDirectory,
            CancellationToken cancellationToken = default) =>
            _inner.FinalizeStagingAsync(stagingPath, finalPath, projectsDirectory, cancellationToken);

        public Task CleanupStagingAsync(
            string stagingPath,
            string projectsDirectory,
            CancellationToken cancellationToken = default) =>
            _inner.CleanupStagingAsync(stagingPath, projectsDirectory, cancellationToken);

        public Task<(string Destination, string? BackupPath)> CopyWithBackupDetailedAsync(
            string sourcePath,
            string destinationRoot,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _inner.CopyWithBackupDetailedAsync(sourcePath, destinationRoot, relativePath, cancellationToken);

        public Task<IReadOnlyList<WorkspaceFileMetadata>> ListFilesAsync(
            string root,
            string relativeDirectory,
            bool recursive = false,
            CancellationToken cancellationToken = default) =>
            _inner.ListFilesAsync(root, relativeDirectory, recursive, cancellationToken);

        public Task<WorkspaceFileMetadata?> GetFileMetadataAsync(
            string root,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _inner.GetFileMetadataAsync(root, relativePath, cancellationToken);

        public Task<bool> FileExistsAsync(
            string root,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _inner.FileExistsAsync(root, relativePath, cancellationToken);

        public Task<string> ResolvePathAsync(
            string root,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _inner.ResolvePathAsync(root, relativePath, cancellationToken);

        public Task<string> ToRelativePathAsync(
            string root,
            string absolutePath,
            CancellationToken cancellationToken = default) =>
            _inner.ToRelativePathAsync(root, absolutePath, cancellationToken);

        public Task<long> GetAvailableFreeSpaceAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            _inner.GetAvailableFreeSpaceAsync(path, cancellationToken);

        public Task DeleteFileAsync(
            string root,
            string relativePath,
            CancellationToken cancellationToken = default) =>
            _inner.DeleteFileAsync(root, relativePath, cancellationToken);

        public Task<string> ComputeChecksumAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new string('0', 64));
    }
}
