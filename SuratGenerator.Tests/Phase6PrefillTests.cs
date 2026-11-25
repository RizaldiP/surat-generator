using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase6PrefillTests
{
    [Fact]
    public void AliasMemetakanPlaceholderDanMengutamakanNilaiManual()
    {
        var service = new PlaceholderAliasService();
        var fields = new[]
        {
            new TemplateField("kapal", "Kapal", 0, TemplateFieldKind.Text),
            new TemplateField("nama pekerjaan", "Pekerjaan", 1, TemplateFieldKind.Text),
            new TemplateField("lokasi", "Lokasi", 2, TemplateFieldKind.Text),
            new TemplateField("nomor unik", "Nomor Unik", 3, TemplateFieldKind.Text)
        };
        var profile = new[]
        {
            new ProjectFieldInfo(Guid.NewGuid(), Guid.NewGuid(), ProfileFieldKeys.ShipName, "KRI Uji", "Text", "Profil"),
            new ProjectFieldInfo(Guid.NewGuid(), Guid.NewGuid(), ProfileFieldKeys.JobName, "Perbaikan", "Text", "Profil"),
            new ProjectFieldInfo(Guid.NewGuid(), Guid.NewGuid(), ProfileFieldKeys.WorkLocation, "Tanjung", "Text", "Profil")
        };
        var saved = new[]
        {
            new DocumentFieldValueInfo(Guid.NewGuid(), Guid.NewGuid(), "ship_name", "KRI Manual", true, DateTimeOffset.UtcNow)
        };
        var mappings = new[]
        {
            new PlaceholderMappingInfo(Guid.NewGuid(), "{nomor unik}", "contract_number", true, DateTimeOffset.UtcNow)
        };

        var result = service.BuildFields(fields, profile, saved, mappings);

        Assert.Equal("KRI Manual", result[0].Value);
        Assert.True(result[0].IsOverride);
        Assert.Equal(ProfileFieldKeys.ShipName, result[0].ProfileFieldKey);
        Assert.Equal("Perbaikan", result[1].Value);
        Assert.Equal("Tanjung", result[2].Value);
        Assert.Equal("contractnumber", result[3].CanonicalKey);
        var values = service.BuildValues(result);
        Assert.Equal("KRI Manual", values["kapal"]);
        Assert.Equal("Perbaikan", values["nama pekerjaan"]);
    }

    [Fact]
    public async Task EditSessionMenyimpanTemplateDasarDanNilaiPerDokumen()
    {
        var environment = CreateEnvironment();
        try
        {
            var (documentId, originalPath) = await CreateWordProjectAsync(environment);
            var originalContent = await File.ReadAllTextAsync(originalPath);
            using var fieldStore = environment.CreateFieldStore();

            var edit = await fieldStore.EnsureEditSessionAsync(documentId);
            Assert.True(File.Exists(edit.BaseTemplatePath));
            Assert.EndsWith(".base.docx", edit.BaseTemplatePath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(environment.ProjectsRoot, edit.OutputPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(edit.BaseTemplatePath));

            await File.WriteAllTextAsync(edit.OutputPath, "hasil edit");
            await fieldStore.SaveAsync(new DocumentFieldEditSaveRequest(
                documentId,
                edit.ProjectId,
                edit.OutputPath,
                new[]
                {
                    new DocumentFieldValueSaveInfo("ship_name", "KRI Manual", true),
                    new DocumentFieldValueSaveInfo("job_name", "Pekerjaan Manual", false)
                }));

            using var projectStore = environment.CreateProjectStore();
            var detail = await projectStore.GetDetailAsync(edit.ProjectId);
            var document = detail!.Documents.Single(item => item.Id == documentId);
            Assert.Equal(DocumentStatuses.InProgress, document.Status);
            Assert.True(document.LastEditedAt.HasValue);
            Assert.Equal(originalContent, await File.ReadAllTextAsync(originalPath));

            var reloaded = await fieldStore.EnsureEditSessionAsync(documentId);
            Assert.Equal(edit.BaseTemplatePath, reloaded.BaseTemplatePath);
            Assert.Equal(edit.OutputPath, reloaded.OutputPath);
            Assert.Contains(reloaded.Values, value => value.Key == "shipname" && value.Value == "KRI Manual" && value.IsOverride);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task SaveMenjagaOverrideManualDanPromoteMenulisProfil()
    {
        var environment = CreateEnvironment();
        try
        {
            var (documentId, _) = await CreateWordProjectAsync(environment);
            using var fieldStore = environment.CreateFieldStore();
            var edit = await fieldStore.EnsureEditSessionAsync(documentId);
            await File.WriteAllTextAsync(edit.OutputPath, "hasil");
            await fieldStore.SaveAsync(new DocumentFieldEditSaveRequest(
                documentId,
                edit.ProjectId,
                edit.OutputPath,
                new[] { new DocumentFieldValueSaveInfo("ship_name", "Manual", true) }));

            await fieldStore.SaveAsync(new DocumentFieldEditSaveRequest(
                documentId,
                edit.ProjectId,
                edit.OutputPath,
                new[] { new DocumentFieldValueSaveInfo("ship_name", "Dari Profil", false) }));

            var current = await fieldStore.EnsureEditSessionAsync(documentId);
            Assert.Contains(current.Values, value => value.Key == "shipname" && value.Value == "Manual");

            await fieldStore.PromoteToProjectFieldsAsync(
                documentId,
                new[] { new ProjectFieldSaveInfo("ship_name", "Manual") });

            using var projectStore = environment.CreateProjectStore();
            var detail = await projectStore.GetDetailAsync(edit.ProjectId);
            Assert.Contains(detail!.Fields, field => field.Key == ProfileFieldKeys.ShipName && field.Value == "Manual");

            var prepared = await fieldStore.PrepareOutputAsync(documentId, edit.OutputPath);
            Assert.Equal(edit.OutputPath, prepared);
            Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(edit.OutputPath)!, "*.bak-*.docx"));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fieldStore.PrepareOutputAsync(documentId, Path.Combine(environment.Directory, "luar.docx")));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    private static async Task<(Guid DocumentId, string OriginalPath)> CreateWordProjectAsync(
        TestEnvironment environment)
    {
        var category = (await environment.Categories.GetAllAsync())[0];
        var source = Path.Combine(environment.Directory, "Surat.docx");
        await File.WriteAllTextAsync(source, "template asli");
        await environment.Masters.UploadAsync(category.Id, source, "Surat Uji", true);

        using var store = environment.CreateProjectStore();
        var creation = await store.CreateAsync(new ProjectCreationRequest(
            "KRI Fase Enam",
            "Uji Editor",
            Array.Empty<ProjectFieldRequest>()));
        Assert.NotNull(creation.Project);
        var detail = await store.GetDetailAsync(creation.Project.Id);
        var document = detail!.Documents.Single(item => item.IsRequired);
        var path = await store.ResolveDocumentPathAsync(document.Id);
        return (document.Id, path);
    }

    private static TestEnvironment CreateEnvironment()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase6Tests", Guid.NewGuid().ToString("N"));
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

        public DocumentFieldValueStore CreateFieldStore() =>
            new(Options, FileService);

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
