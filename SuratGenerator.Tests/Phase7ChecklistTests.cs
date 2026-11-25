using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase7ChecklistTests
{
    [Fact]
    public async Task ItemWajibStoreMenyimpanMengubahDanMenghapusData()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())
                .First(item => string.Equals(item.Name, "Dokumen Tagihan", StringComparison.Ordinal));
            var first = await environment.Checklists.AddAsync(category.Id, "Efaktur");
            var second = await environment.Checklists.AddAsync(category.Id, "SPMK");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                environment.Checklists.AddAsync(category.Id, "efaktur"));

            var renamed = await environment.Checklists.RenameAsync(first.Id, "E-Faktur");
            Assert.Equal("E-Faktur", renamed.DisplayName);
            var inactive = await environment.Checklists.SetActiveAsync(second.Id, false);
            Assert.False(inactive.IsActive);

            var items = await environment.Checklists.GetByCategoryAsync(category.Id);
            Assert.Equal(2, items.Count);
            Assert.Equal("E-Faktur", items[0].DisplayName);
            Assert.Equal("SPMK", items[1].DisplayName);

            await environment.Checklists.DeleteAsync(second.Id);
            Assert.Single(await environment.Checklists.GetByCategoryAsync(category.Id));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task GenerateDanUploadMengisiItemWajibTanpaDuplikat()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())
                .First(item => string.Equals(item.Name, "Dokumen Tagihan", StringComparison.Ordinal));
            await environment.Checklists.AddAsync(category.Id, "Efaktur");

            using var store = environment.CreateProjectStore();
            var creation = await store.CreateAsync(new ProjectCreationRequest(
                "KRI Tujuh",
                "Uji Checklist",
                Array.Empty<ProjectFieldRequest>()));
            Assert.NotNull(creation.Project);

            var detail = await store.GetDetailAsync(creation.Project.Id);
            var requirement = detail!.Documents.Single(document =>
                string.Equals(document.Source, DocumentSources.ChecklistItem, StringComparison.Ordinal));
            Assert.True(requirement.IsRequired);
            Assert.Equal(DocumentStatuses.NotAvailable, requirement.Status);
            Assert.True(string.IsNullOrWhiteSpace(requirement.FilePath));

            var source = Path.Combine(environment.Directory, "Efaktur.pdf");
            await File.WriteAllTextAsync(source, "isi efaktur");
            var upload = await store.UploadDocumentsAsync(
                creation.Project.Id,
                requirement.ProjectCategoryId,
                new[] { source },
                (_, _) => Task.FromResult(ProjectUploadCollisionPolicy.Cancel));
            Assert.Single(upload.Documents);

            var after = await store.GetDetailAsync(creation.Project.Id);
            var filled = after!.Documents.Single(document => document.Id == requirement.Id);
            Assert.Equal(DocumentStatuses.Incomplete, filled.Status);
            Assert.False(string.IsNullOrWhiteSpace(filled.FilePath));
            Assert.Equal("Efaktur", filled.DisplayName);
            Assert.Equal(
                1,
                after.Documents.Count(document =>
                    string.Equals(document.Source, DocumentSources.ChecklistItem, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task SetStatusMenolakSelesaiTanpaFileDanMenerimaDokumenBerkas()
    {
        var environment = CreateEnvironment();
        try
        {
            Directory.CreateDirectory(environment.ProjectsRoot);
            var projectId = Guid.NewGuid();
            var projectCategoryId = Guid.NewGuid();
            var noFileId = Guid.NewGuid();
            var withFileId = Guid.NewGuid();
            var relativePath = Path.Combine("01. Dokumen", "Ada.docx");
            var absolutePath = Path.Combine(environment.ProjectsRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            await File.WriteAllTextAsync(absolutePath, "isi dokumen");
            var now = DateTimeOffset.UtcNow;

            using (var db = new SuratGeneratorDbContext(environment.Options))
            {
                db.Projects.Add(new ProjectEntity
                {
                    Id = projectId,
                    ShipName = "KRI Uji",
                    JobName = "Status",
                    RootPath = environment.ProjectsRoot,
                    Status = ProjectStatuses.Active,
                    StructureVersion = "v3.1",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                db.ProjectCategories.Add(new ProjectCategoryEntity
                {
                    Id = projectCategoryId,
                    ProjectId = projectId,
                    SourceCategoryId = Guid.NewGuid(),
                    Name = "01. Dokumen",
                    SortOrder = 1,
                    FolderPath = "01. Dokumen"
                });
                db.ProjectDocuments.Add(new ProjectDocumentEntity
                {
                    Id = noFileId,
                    ProjectId = projectId,
                    ProjectCategoryId = projectCategoryId,
                    DisplayName = "Belum Ada.pdf",
                    FilePath = string.Empty,
                    FileType = ".pdf",
                    Source = DocumentSources.ChecklistItem,
                    Status = DocumentStatuses.NotAvailable,
                    VersionSnapshot = "checklist",
                    IsRequired = true,
                    FileSizeBytes = 0,
                    ImportedAt = now,
                    UpdatedAt = now
                });
                db.ProjectDocuments.Add(new ProjectDocumentEntity
                {
                    Id = withFileId,
                    ProjectId = projectId,
                    ProjectCategoryId = projectCategoryId,
                    DisplayName = "Ada.docx",
                    FilePath = relativePath,
                    FileType = ".docx",
                    Source = DocumentSources.ManualUpload,
                    Status = DocumentStatuses.Incomplete,
                    VersionSnapshot = "manual",
                    IsRequired = true,
                    FileSizeBytes = 11,
                    ImportedAt = now,
                    UpdatedAt = now
                });
                await db.SaveChangesAsync();
            }

            using var store = environment.CreateProjectStore();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SetDocumentStatusAsync(noFileId, DocumentStatuses.Completed));
            var updated = await store.SetDocumentStatusAsync(withFileId, DocumentStatuses.Completed);
            Assert.Equal(DocumentStatuses.Completed, updated.Status);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task ValidasiPlaceholderHanyaMenghitungFieldYangBelumTerisi()
    {
        var environment = CreateEnvironment();
        try
        {
            var category = (await environment.Categories.GetAllAsync())[0];
            var templatePath = Path.Combine(environment.Directory, "Template.docx");
            CreateTemplate(templatePath, "{lokasi} {nomor}");
            await environment.Masters.UploadAsync(category.Id, templatePath, "Surat Uji", true);

            using var projectStore = environment.CreateProjectStore();
            var creation = await projectStore.CreateAsync(new ProjectCreationRequest(
                "KRI Delapan",
                "Uji Placeholder",
                Array.Empty<ProjectFieldRequest>()));
            var document = (await projectStore.GetDetailAsync(creation.Project!.Id))!
                .Documents.Single(item => item.IsRequired);

            using var fieldStore = environment.CreateFieldStore();
            var edit = await fieldStore.EnsureEditSessionAsync(document.Id);
            await File.WriteAllTextAsync(edit.OutputPath, "hasil");

            var before = await fieldStore.GetUnfilledRequiredPlaceholdersAsync(document.Id);
            Assert.Equal(2, before.Count);
            Assert.Contains("lokasi", before);
            Assert.Contains("nomor", before);

            await fieldStore.SaveAsync(new DocumentFieldEditSaveRequest(
                document.Id,
                edit.ProjectId,
                edit.OutputPath,
                new[] { new DocumentFieldValueSaveInfo("work_location", "Bandung", true) }));

            var after = await fieldStore.GetUnfilledRequiredPlaceholdersAsync(document.Id);
            Assert.Single(after);
            Assert.Equal("nomor", after[0]);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    private static void CreateTemplate(string path, string text)
    {
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
        mainPart.Document.Save();
    }

    private static TestEnvironment CreateEnvironment()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase7Tests", Guid.NewGuid().ToString("N"));
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
        var checklists = new CategoryChecklistItemStore(options);
        return new TestEnvironment(
            directory,
            projectsRoot,
            options,
            pathService,
            fileService,
            categories,
            masters,
            checklists);
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
            MasterDocumentStore masters,
            CategoryChecklistItemStore checklists)
        {
            Directory = directory;
            ProjectsRoot = projectsRoot;
            Options = options;
            PathService = pathService;
            FileService = fileService;
            Categories = categories;
            Masters = masters;
            Checklists = checklists;
        }

        public string Directory { get; }

        public string ProjectsRoot { get; }

        public DbContextOptions<SuratGeneratorDbContext> Options { get; }

        public IWorkspacePathService PathService { get; }

        public IWorkspaceFileService FileService { get; }

        public DocumentCategoryStore Categories { get; }

        public MasterDocumentStore Masters { get; }

        public CategoryChecklistItemStore Checklists { get; }

        public ProjectStore CreateProjectStore() =>
            new(Options, PathService, FileService, Categories, Masters, Checklists);

        public DocumentFieldValueStore CreateFieldStore() =>
            new(Options, FileService);

        public void Dispose()
        {
            Categories.Dispose();
            Masters.Dispose();
            Checklists.Dispose();
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
