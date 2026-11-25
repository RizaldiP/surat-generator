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

    private static Func<ProjectFileCollisionInfo, CancellationToken, Task<ProjectUploadCollisionPolicy>> StaticPolicy(
        ProjectUploadCollisionPolicy policy) =>
        (_, _) => Task.FromResult(policy);

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
