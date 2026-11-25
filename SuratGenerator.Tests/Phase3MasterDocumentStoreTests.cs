using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase3MasterDocumentStoreTests
{
    [Fact]
    public async Task UploadMenyalinFileDanMembuatVersiAwal()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var category = (await categories.GetAllAsync())[0];
            var pathService = new TestWorkspacePathService(masterRoot, directory);
            using var store = new MasterDocumentStore(options, pathService);
            var sourcePath = Path.Combine(directory, "surat.docx");
            await File.WriteAllTextAsync(sourcePath, "isi master awal");

            var document = await store.UploadAsync(category.Id, sourcePath, "Surat Pokok", true);

            Assert.Equal("Surat Pokok", document.DisplayName);
            Assert.True(document.IsRequired);
            Assert.True(document.IsActive);
            Assert.False(document.IsArchived);
            Assert.Equal("1.0", document.CurrentVersionNumber);
            Assert.Equal(64, document.Checksum!.Length);
            Assert.False(Path.IsPathRooted(document.StoredFilePath));
            Assert.Equal("isi master awal", await File.ReadAllTextAsync(Path.Combine(masterRoot, document.StoredFilePath)));

            var versions = await store.GetVersionsAsync(document.Id);
            Assert.Single(versions);
            Assert.Equal("1.0", versions[0].VersionNumber);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.UploadAsync(category.Id, sourcePath, "surat pokok", false));

            var invalidPath = Path.Combine(directory, "data.txt");
            await File.WriteAllTextAsync(invalidPath, "tipe tidak didukung");
            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.UploadAsync(category.Id, invalidPath, "Data", false));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task GantiFileMenambahVersiDanArsipMenonaktifkan()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var category = (await categories.GetAllAsync())[0];
            var pathService = new TestWorkspacePathService(masterRoot, directory);
            using var store = new MasterDocumentStore(options, pathService);
            var firstPath = Path.Combine(directory, "awal.docx");
            var secondPath = Path.Combine(directory, "baru.docx");
            await File.WriteAllTextAsync(firstPath, "versi awal");
            await File.WriteAllTextAsync(secondPath, "versi baru");
            var document = await store.UploadAsync(category.Id, firstPath, "Format Surat", false);

            var replaced = await store.ReplaceVersionAsync(document.Id, secondPath);

            Assert.Equal("1.1", replaced.CurrentVersionNumber);
            Assert.NotEqual(document.StoredFilePath, replaced.StoredFilePath);
            Assert.True(File.Exists(Path.Combine(masterRoot, document.StoredFilePath)));
            Assert.True(File.Exists(Path.Combine(masterRoot, replaced.StoredFilePath)));
            Assert.Equal(2, (await store.GetVersionsAsync(document.Id)).Count);

            var archived = await store.SetArchivedAsync(document.Id, true);
            Assert.True(archived.IsArchived);
            Assert.False(archived.IsActive);

            var restored = await store.RestoreAsync(document.Id);
            Assert.False(restored.IsArchived);
            Assert.True(restored.IsActive);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task UpdateMetadataDapatMemindahKategoriDanFile()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var sourceCategory = (await categories.GetAllAsync())[0];
            var targetCategory = await categories.AddAsync("Kategori Kedua");
            var pathService = new TestWorkspacePathService(masterRoot, directory);
            using var store = new MasterDocumentStore(options, pathService);
            var sourcePath = Path.Combine(directory, "lampiran.docx");
            await File.WriteAllTextAsync(sourcePath, "isi lampiran");
            var document = await store.UploadAsync(sourceCategory.Id, sourcePath, "Lampiran", true);

            var moved = await store.UpdateMetadataAsync(
                document.Id,
                displayName: "Lampiran Utama",
                isRequired: false,
                categoryId: targetCategory.Id);

            Assert.Equal(targetCategory.Id, moved.CategoryId);
            Assert.Equal("Lampiran Utama", moved.DisplayName);
            Assert.False(moved.IsRequired);
            Assert.StartsWith(targetCategory.Id.ToString("N"), moved.StoredFilePath, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(masterRoot, moved.StoredFilePath)));
            Assert.False(Directory.Exists(Path.Combine(masterRoot, sourceCategory.Id.ToString("N"), document.Id.ToString("N"))));

            var versions = await store.GetVersionsAsync(document.Id);
            Assert.All(versions, version =>
                Assert.StartsWith(targetCategory.Id.ToString("N"), version.StoredFilePath, StringComparison.Ordinal));
            Assert.True(File.Exists(await store.ResolveOpenPathAsync(document.Id)));
            Assert.Single(await store.GetByCategoryAsync(targetCategory.Id));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task RelocateMemindahkanFileDanMenormalkanPathLama()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var oldRoot = Path.Combine(directory, "MasterLama");
        var newRoot = Path.Combine(directory, "MasterBaru");
        var options = CreateOptions(databasePath);

        try
        {
            using var categories = new DocumentCategoryStore(options);
            var category = (await categories.GetAllAsync())[0];
            var pathService = new TestWorkspacePathService(oldRoot, directory);
            using var store = new MasterDocumentStore(options, pathService);
            var sourcePath = Path.Combine(directory, "panduan.docx");
            await File.WriteAllTextAsync(sourcePath, "isi panduan");
            var document = await store.UploadAsync(category.Id, sourcePath, "Panduan", false);
            var oldAbsolute = Path.Combine(oldRoot, document.StoredFilePath);

            using (var context = new SuratGeneratorDbContext(options))
            {
                var entity = await context.MasterDocuments.FirstAsync(item => item.Id == document.Id);
                entity.StoredFilePath = oldAbsolute;
                await context.SaveChangesAsync();
            }

            await store.RelocateAsync(oldRoot, newRoot);
            await pathService.SetAsync(
                newRoot,
                Path.Combine(directory, "Projects"),
                Path.Combine(directory, "Backups"));

            using (var context = new SuratGeneratorDbContext(options))
            {
                var entity = await context.MasterDocuments.FirstAsync(item => item.Id == document.Id);
                Assert.False(Path.IsPathRooted(entity.StoredFilePath));
                Assert.Equal(document.StoredFilePath, entity.StoredFilePath);
            }

            var resolved = await store.ResolveOpenPathAsync(document.Id);
            Assert.Equal(Path.Combine(newRoot, document.StoredFilePath), resolved);
            Assert.True(File.Exists(resolved));
            Assert.Equal("isi panduan", await File.ReadAllTextAsync(resolved));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase3Tests", Guid.NewGuid().ToString("N"));
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

    private sealed class TestWorkspacePathService : IWorkspacePathService
    {
        private WorkspacePathsInfo _paths;

        public TestWorkspacePathService(string masterRoot, string directory)
        {
            _paths = new WorkspacePathsInfo(
                masterRoot,
                Path.Combine(directory, "Projects"),
                Path.Combine(directory, "Backups"));
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
