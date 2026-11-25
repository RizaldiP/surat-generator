using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase5ProjectDetailTests
{
    [Fact]
    public async Task UploadMenambahDokumenManualDanMenggantiDenganBackup()
    {
        var environment = CreateEnvironment();
        try
        {
            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var category = (await store.GetDetailAsync(project.Id))!.Categories[0];
            var first = CreateSource(environment.Directory, "first", "lampiran.pdf", "isi awal");
            var second = CreateSource(environment.Directory, "second", "lampiran.pdf", "isi baru");

            var firstResult = await store.UploadDocumentsAsync(
                project.Id,
                category.Id,
                new[] { first },
                StaticPolicy(ProjectUploadCollisionPolicy.Cancel));
            var secondResult = await store.UploadDocumentsAsync(
                project.Id,
                category.Id,
                new[] { second },
                StaticPolicy(ProjectUploadCollisionPolicy.ReplaceWithBackup));

            Assert.Single(firstResult.Documents);
            Assert.Equal(DocumentSources.ManualUpload, firstResult.Documents[0].Source);
            Assert.Equal(DocumentStatuses.Incomplete, firstResult.Documents[0].Status);
            Assert.False(firstResult.Documents[0].IsRequired);
            Assert.Single(secondResult.Documents);
            var documentPath = await store.ResolveDocumentPathAsync(secondResult.Documents[0].Id);
            Assert.Equal("isi baru", await File.ReadAllTextAsync(documentPath));
            var categoryPath = await store.ResolveCategoryPathAsync(category.Id);
            Assert.Single(Directory.GetFiles(categoryPath, "*.bak-*"));
            var refresh = await store.RefreshFilesystemAsync(project.Id);
            Assert.DoesNotContain(refresh.Changes, change => change.Action == "Ditambahkan");
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task UploadCollisionNamaBaruDanBatalTidakMenimpa()
    {
        var environment = CreateEnvironment();
        try
        {
            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var category = (await store.GetDetailAsync(project.Id))!.Categories[0];
            var first = CreateSource(environment.Directory, "first", "catatan.pdf", "awal");
            var second = CreateSource(environment.Directory, "second", "catatan.pdf", "baru");
            var third = CreateSource(environment.Directory, "third", "catatan.pdf", "ketiga");

            await store.UploadDocumentsAsync(project.Id, category.Id, new[] { first }, StaticPolicy(ProjectUploadCollisionPolicy.Cancel));
            var renamed = await store.UploadDocumentsAsync(
                project.Id,
                category.Id,
                new[] { second },
                StaticPolicy(ProjectUploadCollisionPolicy.SaveAsDifferentName));
            var cancelled = await store.UploadDocumentsAsync(
                project.Id,
                category.Id,
                new[] { third },
                StaticPolicy(ProjectUploadCollisionPolicy.Cancel));

            Assert.Contains("(2)", renamed.Documents[0].FilePath);
            Assert.Equal("baru", await File.ReadAllTextAsync(await store.ResolveDocumentPathAsync(renamed.Documents[0].Id)));
            Assert.Single(cancelled.SkippedFiles);
            var detail = await store.GetDetailAsync(project.Id);
            Assert.Equal(2, detail!.Documents.Count);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RefreshMerekonsiliasiFileExplorerTanpaMenghapusCatatan()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "panduan.docx");
            await File.WriteAllTextAsync(source, "isi panduan");
            await environment.Masters.UploadAsync(category.Id, source, "Panduan Wajib", true);

            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var detailBefore = await store.GetDetailAsync(project.Id);
            var masterDocument = detailBefore!.Documents.Single(document => document.IsRequired);
            var masterPath = await store.ResolveDocumentPathAsync(masterDocument.Id);
            var categoryPath = Path.GetDirectoryName(masterPath)!;

            await File.WriteAllTextAsync(Path.Combine(categoryPath, "dari-explorer.pdf"), "isi explorer");
            File.Delete(masterPath);

            var refresh = await store.RefreshFilesystemAsync(project.Id);

            Assert.Contains(refresh.Changes, change => change.Action == "Ditambahkan");
            Assert.Contains(refresh.Changes, change => change.Action == "Tidak Tersedia");
            Assert.Contains(refresh.Detail.Documents, document =>
                document.DisplayName == "dari-explorer.pdf"
                && document.Source == DocumentSources.ManualUpload
                && document.Status == DocumentStatuses.Incomplete);
            var updatedMaster = refresh.Detail.Documents.Single(document => document.Id == masterDocument.Id);
            Assert.Equal(DocumentStatuses.NotAvailable, updatedMaster.Status);
            Assert.NotEmpty(updatedMaster.FilePath);
            Assert.Equal(1, refresh.Detail.Checklist.RequiredCount);
            Assert.Equal(0, refresh.Detail.Checklist.ProgressPercent);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task PathResolverMengembalikanPathValidDanMenolakDokumenTanpaFile()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "arsip.docx");
            await File.WriteAllTextAsync(source, "master arsip");
            var archived = await environment.Masters.UploadAsync(category.Id, source, "Master Arsip", true);
            await environment.Masters.SetArchivedAsync(archived.Id, true);

            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var detail = await store.GetDetailAsync(project.Id);
            var projectCategory = detail!.Categories[0];
            var root = await store.ResolveProjectPathAsync(project.Id);
            var categoryPath = await store.ResolveCategoryPathAsync(projectCategory.Id);

            Assert.Equal(detail.Project.RootPath, root);
            Assert.StartsWith(root, categoryPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(categoryPath));

            var missing = detail.Documents.Single(document => string.IsNullOrWhiteSpace(document.FilePath));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.ResolveDocumentPathAsync(missing.Id));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RefreshMenarikMasterBaruDanMendeteksiFolderHilang()
    {
        var environment = CreateEnvironment();
        try
        {
            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "tagihan.docx");
            await File.WriteAllTextAsync(source, "isi tagihan");
            var master = await environment.Masters.UploadAsync(category.Id, source, "Tagihan Baru", true);

            var before = await store.GetDetailAsync(project.Id);
            Assert.DoesNotContain(before!.Documents, document => document.MasterDocumentId == master.Id);

            var refresh = await store.RefreshFilesystemAsync(project.Id, pullMissingMasters: true);
            Assert.Contains(refresh.Changes, change => change.Action == "Master Ditarik");
            var pulled = refresh.Detail.Documents.Single(document => document.MasterDocumentId == master.Id);
            Assert.Equal(DocumentSources.MasterCopy, pulled.Source);
            Assert.Equal(DocumentStatuses.Incomplete, pulled.Status);
            Assert.True(File.Exists(await store.ResolveDocumentPathAsync(pulled.Id)));

            Directory.Delete(project.RootPath, true);
            Assert.Empty(await store.GetAllAsync());
            var hidden = Assert.Single(await store.GetAllAsync(includeMissing: true));
            Assert.True(hidden.IsMissing);

            Directory.CreateDirectory(project.RootPath);
            var restored = Assert.Single(await store.GetAllAsync());
            Assert.False(restored.IsMissing);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task DeleteAsyncMenghapusMetadataDanMenyimpanFolder()
    {
        var environment = CreateEnvironment();
        try
        {
            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);

            Assert.True(await store.DeleteAsync(project.Id));
            Assert.Null(await store.GetDetailAsync(project.Id));
            Assert.Empty(await store.GetAllAsync(includeMissing: true));
            Assert.True(Directory.Exists(project.RootPath));
            Assert.False(await store.DeleteAsync(project.Id));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RefreshMengadopsiFileExistingDanTidakMenyalinDuplikat()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "adopsi.docx");
            await File.WriteAllTextAsync(source, "isi master");
            var master = await environment.Masters.UploadAsync(category.Id, source, "Adopsi Master", true);
            await environment.Masters.SetActiveAsync(master.Id, false);

            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var detailBefore = await store.GetDetailAsync(project.Id);
            Assert.Empty(detailBefore!.Warnings ?? []);

            var categoryPath = await store.ResolveCategoryPathAsync(detailBefore.Categories[0].Id);
            var canonicalPath = Path.Combine(categoryPath, "adopsi.docx");
            await File.WriteAllTextAsync(canonicalPath, "isi manual");
            await environment.Masters.SetActiveAsync(master.Id, true);

            var refresh = await store.RefreshFilesystemAsync(project.Id, pullMissingMasters: true);
            Assert.Contains(refresh.Changes, change => change.Action == "Master Diadopsi");
            var adopted = Assert.Single(refresh.Detail.Documents, document => document.MasterDocumentId == master.Id);
            Assert.EndsWith("adopsi.docx", adopted.FilePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(DocumentStatuses.Incomplete, adopted.Status);
            Assert.Equal("isi manual", await File.ReadAllTextAsync(await store.ResolveDocumentPathAsync(adopted.Id)));
            Assert.Empty(Directory.GetFiles(categoryPath, "*adopsi (2)*"));
            Assert.Empty(refresh.Detail.Warnings ?? []);

            var fileCount = Directory.GetFiles(categoryPath).Length;
            var secondRefresh = await store.RefreshFilesystemAsync(project.Id, pullMissingMasters: true);
            Assert.DoesNotContain(secondRefresh.Changes, change => change.Action is "Master Diadopsi" or "Master Ditarik");
            Assert.Equal(fileCount, Directory.GetFiles(categoryPath).Length);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RefreshMenghapusSalinanMasterIdentikYangBertumpuk()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "tagihan.docx");
            await File.WriteAllTextAsync(source, "isi tagihan");
            var master = await environment.Masters.UploadAsync(category.Id, source, "Tagihan", true);

            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var detail = await store.GetDetailAsync(project.Id);
            var projectRoot = await store.ResolveProjectPathAsync(project.Id);
            var original = Assert.Single(detail!.Documents, document => document.MasterDocumentId == master.Id);
            await AddDuplicateMasterCopyAsync(environment, projectRoot, original, "tagihan (2).docx", "isi tagihan");
            await AddDuplicateMasterCopyAsync(environment, projectRoot, original, "tagihan (3).docx", "isi tagihan");

            var refresh = await store.RefreshFilesystemAsync(project.Id, pullMissingMasters: true);
            Assert.Equal(2, refresh.Changes.Count(change => change.Action == "Duplikat Dihapus"));
            Assert.Single(refresh.Detail.Documents, document => document.MasterDocumentId == master.Id);
            var categoryPath = await store.ResolveCategoryPathAsync(detail.Categories[0].Id);
            Assert.Single(Directory.GetFiles(categoryPath, "tagihan*.docx"));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RefreshMempertahankanSalinanMasterBedaChecksumDanMemberiPeringatan()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "izin.docx");
            await File.WriteAllTextAsync(source, "isi master");
            var master = await environment.Masters.UploadAsync(category.Id, source, "Izin", true);

            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var detail = await store.GetDetailAsync(project.Id);
            var projectRoot = await store.ResolveProjectPathAsync(project.Id);
            var original = Assert.Single(detail!.Documents, document => document.MasterDocumentId == master.Id);
            await AddDuplicateMasterCopyAsync(environment, projectRoot, original, "izin (2).docx", "isi berbeda");

            var refresh = await store.RefreshFilesystemAsync(project.Id, pullMissingMasters: true);
            Assert.DoesNotContain(refresh.Changes, change => change.Action == "Duplikat Dihapus");
            Assert.Equal(2, refresh.Detail.Documents.Count(document => document.MasterDocumentId == master.Id));
            Assert.Contains(refresh.Detail.Warnings ?? [], warning => warning.Contains("isi berbeda", StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task RefreshMenarikMasterDanMenyinkronkanChecksumYangBerubah()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "izin.docx");
            await File.WriteAllTextAsync(source, "isi lama");
            var master = await environment.Masters.UploadAsync(category.Id, source, "Izin", true);

            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var detail = await store.GetDetailAsync(project.Id);
            var document = Assert.Single(detail!.Documents, item => item.MasterDocumentId == master.Id);
            var copiedPath = await store.ResolveDocumentPathAsync(document.Id);
            File.Delete(copiedPath);

            var paths = await environment.PathService.GetAsync();
            var storedPath = Path.Combine(paths.MasterDocumentsDirectory, master.StoredFilePath);
            await File.WriteAllTextAsync(storedPath, "isi baru master");
            var expected = await environment.FileService.ComputeChecksumAsync(storedPath);

            var refresh = await store.RefreshFilesystemAsync(project.Id, pullMissingMasters: true);

            Assert.Contains(refresh.Changes, change => change.Action == "Master Ditarik");
            var current = Assert.Single(await environment.Masters.GetAllAsync(), item => item.Id == master.Id);
            Assert.Equal(expected, current.Checksum);
            Assert.Contains(refresh.Detail.Documents, item =>
                item.MasterDocumentId == master.Id
                && item.VersionSnapshot.Contains(expected[..12], StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task DeleteDocumentMenghapusFileDanCatatanSertaFieldValue()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var source = Path.Combine(environment.Directory, "hapus.docx");
            await File.WriteAllTextAsync(source, "isi hapus");
            await environment.Masters.UploadAsync(category.Id, source, "Hapus", true);

            using var store = environment.CreateProjectStore();
            var project = await CreateProjectAsync(store);
            var detail = await store.GetDetailAsync(project.Id);
            var document = detail!.Documents[0];
            var path = await store.ResolveDocumentPathAsync(document.Id);
            Assert.True(File.Exists(path));

            using (var db = new SuratGeneratorDbContext(environment.Options))
            {
                db.DocumentFieldValues.Add(new DocumentFieldValueEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectDocumentId = document.Id,
                    Key = "catatan",
                    Value = "isi",
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            Assert.True(await store.DeleteDocumentAsync(document.Id));
            Assert.False(File.Exists(path));
            Assert.DoesNotContain((await store.GetDetailAsync(project.Id))!.Documents, item => item.Id == document.Id);
            Assert.False(await store.DeleteDocumentAsync(document.Id));

            using (var db = new SuratGeneratorDbContext(environment.Options))
            {
                Assert.Empty(await db.DocumentFieldValues.ToListAsync());
            }
        }
        finally
        {
            Cleanup(environment);
        }
    }

    private static Func<ProjectFileCollisionInfo, CancellationToken, Task<ProjectUploadCollisionPolicy>> StaticPolicy(
        ProjectUploadCollisionPolicy policy) =>
        (_, _) => Task.FromResult(policy);

    private static async Task AddDuplicateMasterCopyAsync(
        TestEnvironment environment,
        string projectRoot,
        ProjectDocumentInfo source,
        string fileName,
        string content)
    {
        var relativePath = Path.Combine(Path.GetDirectoryName(source.FilePath)!, fileName);
        var destination = Path.Combine(projectRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(destination, content);
        using var db = new SuratGeneratorDbContext(environment.Options);
        db.ProjectDocuments.Add(new ProjectDocumentEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = source.ProjectId,
            ProjectCategoryId = source.ProjectCategoryId,
            MasterDocumentId = source.MasterDocumentId,
            MasterVersionId = source.MasterVersionId,
            DisplayName = source.DisplayName,
            FilePath = relativePath,
            FileType = Path.GetExtension(fileName),
            Source = DocumentSources.MasterCopy,
            Status = DocumentStatuses.Incomplete,
            VersionSnapshot = source.VersionSnapshot,
            IsRequired = source.IsRequired,
            FileSizeBytes = content.Length,
            ImportedAt = source.ImportedAt,
            UpdatedAt = source.UpdatedAt
        });
        await db.SaveChangesAsync();
    }

    private static async Task<ProjectInfo> CreateProjectAsync(ProjectStore store)
    {
        var result = await store.CreateAsync(new ProjectCreationRequest(
            "KRI Fase Lima",
            "Uji Detail",
            Array.Empty<ProjectFieldRequest>()));
        Assert.NotNull(result.Project);
        return result.Project;
    }

    private static string CreateSource(string directory, string subfolder, string fileName, string content)
    {
        var folder = Path.Combine(directory, "Sources", subfolder);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static TestEnvironment CreateEnvironment()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase5Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var options = new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")}")
            .Options;
        var masterRoot = Path.Combine(directory, "MasterDocuments");
        var projectsRoot = Path.Combine(directory, "Projects");
        var pathService = new TestWorkspacePathService(masterRoot, projectsRoot);
        var fileService = new WorkspaceFileService();
        var categories = new DocumentCategoryStore(options);
        var masters = new MasterDocumentStore(options, pathService, fileService);
        return new TestEnvironment(directory, projectsRoot, options, pathService, fileService, categories, masters);
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

    private sealed class TestEnvironment : IDisposable
    {
        public TestEnvironment(
            string directory,
            string projectsRoot,
            DbContextOptions<SuratGeneratorDbContext> options,
            IWorkspacePathService pathService,
            IWorkspaceFileService fileService,
            DocumentCategoryStore categories,
            MasterDocumentStore masters)
        {
            Directory = directory;
            ProjectsRoot = projectsRoot;
            Options = options;
            PathService = pathService;
            FileService = fileService;
            Categories = categories;
            Masters = masters;
        }

        public string Directory { get; }

        public string ProjectsRoot { get; }

        public DbContextOptions<SuratGeneratorDbContext> Options { get; }

        public IWorkspacePathService PathService { get; }

        public IWorkspaceFileService FileService { get; }

        public DocumentCategoryStore Categories { get; }

        public MasterDocumentStore Masters { get; }

        public ProjectStore CreateProjectStore() =>
            new(Options, PathService, FileService, Categories, Masters);

        public void Dispose()
        {
            Categories.Dispose();
            Masters.Dispose();
        }
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
