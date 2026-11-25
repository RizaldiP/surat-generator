using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class ProjectStore : IProjectStore, IDisposable
{
    private static readonly HashSet<string> AllowedUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".doc", ".xlsx", ".xls", ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".gif"
    };

    private readonly SuratGeneratorDbContext _dbContext;
    private readonly IWorkspacePathService _pathService;
    private readonly IWorkspaceFileService _fileService;
    private readonly IDocumentCategoryStore _categoryStore;
    private readonly IMasterDocumentStore _masterStore;
    private readonly ICategoryChecklistItemStore _checklistItemStore;
    private readonly IAppLogger _logger;
    private readonly IReadOnlyList<IDisposable> _ownedServices;

    public ProjectStore(IAppLogger? logger = null)
        : this(
            DatabaseOptionsFactory.CreateProduction(),
            new WorkspacePathService(),
            new WorkspaceFileService(),
            new DocumentCategoryStore(logger),
            new MasterDocumentStore(logger),
            new CategoryChecklistItemStore(),
            logger,
            ownsServices: true)
    {
    }

    public ProjectStore(
        IAppLogger? logger,
        IWorkspacePathService pathService,
        IWorkspaceFileService fileService,
        IDocumentCategoryStore categoryStore,
        IMasterDocumentStore masterStore,
        ICategoryChecklistItemStore? checklistItemStore = null)
        : this(
            DatabaseOptionsFactory.CreateProduction(),
            pathService,
            fileService,
            categoryStore,
            masterStore,
            checklistItemStore,
            logger)
    {
    }

    public ProjectStore(
        DbContextOptions<SuratGeneratorDbContext> options,
        IWorkspacePathService pathService,
        IWorkspaceFileService fileService,
        IDocumentCategoryStore categoryStore,
        IMasterDocumentStore masterStore,
        ICategoryChecklistItemStore? checklistItemStore = null,
        IAppLogger? logger = null,
        bool ownsServices = false)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        _pathService = pathService;
        _fileService = fileService;
        _categoryStore = categoryStore;
        _masterStore = masterStore;
        _checklistItemStore = checklistItemStore ?? new CategoryChecklistItemStore(options);
        _logger = logger ?? NullAppLogger.Instance;
        var owned = new List<IDisposable>();
        if (ownsServices)
        {
            owned.Add((IDisposable)categoryStore);
            owned.Add((IDisposable)masterStore);
        }

        if (checklistItemStore is null)
        {
            owned.Add((IDisposable)_checklistItemStore);
        }

        _ownedServices = owned;
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<ProjectCreationResult> CreateAsync(
        ProjectCreationRequest request,
        IProgress<ProjectCreationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var shipName = ValidateName(request.ShipName, "Nama kapal");
        var jobName = ValidateName(request.JobName, "Nama pekerjaan");
        var paths = await _pathService.GetAsync(cancellationToken);
        var projectsRoot = PathSafety.NormalizeRoot(paths.ProjectsDirectory);
        Directory.CreateDirectory(projectsRoot);
        var folderName = _pathService.BuildProjectFolderName(shipName, jobName);
        var finalPath = Path.GetFullPath(Path.Combine(projectsRoot, folderName));

        Report(progress, "Validasi", 5, "Memeriksa data dan duplikasi pekerjaan.");
        var duplicate = await FindDuplicateCoreAsync(shipName, jobName, finalPath, cancellationToken);
        if (duplicate is not null)
        {
            return new ProjectCreationResult(
                null,
                true,
                duplicate.ProjectId,
                duplicate.RootPath,
                $"Pekerjaan dengan nama atau folder yang sama sudah ada: {duplicate.RootPath}");
        }

        Report(progress, "Struktur", 15, "Membaca snapshot struktur dan master dokumen.");
        var snapshot = await _categoryStore.GetSnapshotAsync(cancellationToken);
        var allMasters = await _masterStore.GetAllAsync(cancellationToken);
        var activeMasters = allMasters
            .Where(master => master.IsActive && !master.IsArchived)
            .ToArray();
        var snapshotCategoryIds = snapshot.Categories
            .Select(category => category.Id)
            .ToHashSet();

        var stagingPath = await _fileService.CreateStagingDirectoryAsync(
            projectsRoot,
            folderName,
            cancellationToken);
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var categoryEntities = new List<ProjectCategoryEntity>();
        var documentEntities = new List<ProjectDocumentEntity>();
        var copiedMasterIds = new HashSet<Guid>();
        var finalized = false;

        try
        {
            for (var index = 0; index < snapshot.Categories.Count; index++)
            {
                var category = snapshot.Categories[index];
                var sortOrder = index + 1;
                var categoryFolder = $"{sortOrder:00}. {category.Name}";
                Directory.CreateDirectory(Path.Combine(stagingPath, categoryFolder));
                categoryEntities.Add(new ProjectCategoryEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    SourceCategoryId = category.Id,
                    Name = categoryFolder,
                    SortOrder = sortOrder,
                    FolderPath = categoryFolder
                });

                var masters = activeMasters
                    .Where(master => master.CategoryId == category.Id)
                    .OrderBy(master => master.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
                foreach (var master in masters)
                {
                    var relativePath = await CopyMasterAsync(
                        master,
                        stagingPath,
                        categoryFolder,
                        cancellationToken);
                    copiedMasterIds.Add(master.Id);
                    var copiedMetadata = await _fileService.GetFileMetadataAsync(stagingPath, relativePath, cancellationToken);
                    documentEntities.Add(new ProjectDocumentEntity
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProjectCategoryId = categoryEntities[^1].Id,
                        MasterDocumentId = master.Id,
                        MasterVersionId = master.CurrentVersionId,
                        DisplayName = master.DisplayName,
                        FilePath = relativePath,
                        FileType = Path.GetExtension(relativePath),
                        Source = DocumentSources.MasterCopy,
                        Status = DocumentStatuses.Incomplete,
                        VersionSnapshot = BuildVersionSnapshot(master.CurrentVersionNumber, master.Checksum),
                        IsRequired = master.IsRequired,
                        FileSizeBytes = copiedMetadata?.Length ?? master.FileSizeBytes,
                        LastModifiedUtc = copiedMetadata?.LastWriteTimeUtc ?? master.UpdatedAt,
                        ImportedAt = now,
                        UpdatedAt = now
                    });
                }

                Report(
                    progress,
                    "Menyalin",
                    20 + ((index + 1) * 60 / Math.Max(1, snapshot.Categories.Count)),
                    $"Menyiapkan {categoryFolder}.");
            }

            foreach (var required in allMasters.Where(master =>
                         master.IsRequired
                         && !copiedMasterIds.Contains(master.Id)
                         && snapshotCategoryIds.Contains(master.CategoryId)))
            {
                var targetCategory = categoryEntities.First(category => category.SourceCategoryId == required.CategoryId);
                documentEntities.Add(new ProjectDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    ProjectCategoryId = targetCategory.Id,
                    MasterDocumentId = required.Id,
                    MasterVersionId = required.CurrentVersionId,
                    DisplayName = required.DisplayName,
                    FilePath = string.Empty,
                    FileType = required.FileType,
                    Source = DocumentSources.MasterCopy,
                        Status = DocumentStatuses.NotAvailable,
                        VersionSnapshot = BuildVersionSnapshot(required.CurrentVersionNumber, required.Checksum),
                        IsRequired = true,
                        FileSizeBytes = 0,
                        LastModifiedUtc = null,
                        ImportedAt = now,
                        UpdatedAt = now
                });
            }

            var checklistItems = (await _checklistItemStore.GetAllAsync(cancellationToken))
                .Where(item => item.IsActive && snapshotCategoryIds.Contains(item.CategoryId))
                .ToArray();
            foreach (var item in checklistItems)
            {
                var targetCategory = categoryEntities.First(category => category.SourceCategoryId == item.CategoryId);
                documentEntities.Add(new ProjectDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    ProjectCategoryId = targetCategory.Id,
                    MasterDocumentId = null,
                    MasterVersionId = null,
                    DisplayName = item.DisplayName,
                    FilePath = string.Empty,
                    FileType = string.Empty,
                    Source = DocumentSources.ChecklistItem,
                    Status = DocumentStatuses.NotAvailable,
                    VersionSnapshot = "checklist",
                    IsRequired = true,
                    FileSizeBytes = 0,
                    LastModifiedUtc = null,
                    ImportedAt = now,
                    UpdatedAt = now
                });
            }

            var fields = BuildFieldEntities(projectId, shipName, jobName, request.Fields);
            var projectEntity = new ProjectEntity
            {
                Id = projectId,
                ShipName = shipName,
                JobName = jobName,
                RootPath = finalPath,
                Status = ProjectStatuses.Active,
                StructureVersion = snapshot.Version,
                CreatedAt = now,
                UpdatedAt = now
            };

            Report(progress, "Database", 85, "Menyimpan metadata pekerjaan.");
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _dbContext.Projects.Add(projectEntity);
                _dbContext.ProjectFields.AddRange(fields);
                _dbContext.ProjectCategories.AddRange(categoryEntities);
                _dbContext.ProjectDocuments.AddRange(documentEntities);
                await _dbContext.SaveChangesAsync(cancellationToken);

                Report(progress, "Finalisasi", 92, "Memindahkan folder dari staging.");
                await _fileService.FinalizeStagingAsync(stagingPath, finalPath, projectsRoot, cancellationToken);
                finalized = true;
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            Report(progress, "Selesai", 100, "Pekerjaan berhasil dibuat.");
            return new ProjectCreationResult(Map(projectEntity), false, null, finalPath, "Pekerjaan berhasil dibuat.");
        }
        catch
        {
            if (finalized && Directory.Exists(finalPath))
            {
                TryDeleteDirectory(finalPath);
            }

            if (!finalized)
            {
                await TryCleanupStagingAsync(stagingPath, projectsRoot);
            }

            throw;
        }
    }

    public async Task<ProjectDuplicateInfo?> FindDuplicateAsync(
        string shipName,
        string jobName,
        CancellationToken cancellationToken = default)
    {
        var paths = await _pathService.GetAsync(cancellationToken);
        var projectsRoot = PathSafety.NormalizeRoot(paths.ProjectsDirectory);
        var folderName = _pathService.BuildProjectFolderName(shipName, jobName);
        var finalPath = Path.GetFullPath(Path.Combine(projectsRoot, folderName));
        return await FindDuplicateCoreAsync(shipName, jobName, finalPath, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return projects
            .OrderByDescending(project => project.UpdatedAt)
            .Select(Map)
            .ToArray();
    }

    public async Task<ProjectDetailInfo?> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        await BuildDetailAsync(id, cancellationToken);

    public async Task<ProjectUploadResult> UploadDocumentsAsync(
        Guid projectId,
        Guid projectCategoryId,
        IReadOnlyList<string> sourcePaths,
        Func<ProjectFileCollisionInfo, CancellationToken, Task<ProjectUploadCollisionPolicy>> resolveCollision,
        IProgress<ProjectCreationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourcePaths is null)
        {
            throw new ArgumentNullException(nameof(sourcePaths));
        }

        if (resolveCollision is null)
        {
            throw new ArgumentNullException(nameof(resolveCollision));
        }

        var project = await GetProjectEntityAsync(projectId, cancellationToken);
        var category = await _dbContext.ProjectCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == projectCategoryId && item.ProjectId == projectId,
                cancellationToken);
        if (category is null)
        {
            throw new InvalidOperationException("Kategori pekerjaan tidak ditemukan.");
        }

        var root = PathSafety.NormalizeRoot(project.RootPath);
        Directory.CreateDirectory(Path.Combine(root, category.FolderPath));
        var knownDocuments = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .Where(document => document.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var createdPaths = new List<string>();
        var documents = new List<ProjectDocumentInfo>();
        var skipped = new List<string>();
        var total = Math.Max(1, sourcePaths.Count);

        Report(progress, "Validasi", 5, "Memeriksa file yang akan diunggah.");
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            for (var index = 0; index < sourcePaths.Count; index++)
            {
                var sourcePath = sourcePaths[index];
                ValidateUploadSource(sourcePath);
                var sourceInfo = new FileInfo(sourcePath);
                var available = await _fileService.GetAvailableFreeSpaceAsync(root, cancellationToken);
                if (available < sourceInfo.Length)
                {
                    throw new IOException("Ruang disk tidak mencukupi untuk mengunggah file.");
                }

                var fileName = PathSafety.SanitizeName(sourceInfo.Name, '_', 120, false);
                var relativePath = Path.Combine(category.FolderPath, fileName);
                var pendingRequirement = knownDocuments.FirstOrDefault(document =>
                    document.ProjectCategoryId == projectCategoryId
                    && string.Equals(document.Source, DocumentSources.ChecklistItem, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(document.FilePath)
                    && MatchesDisplayName(document.DisplayName, fileName));
                var existingDocument = knownDocuments.FirstOrDefault(document =>
                    string.Equals(document.FilePath, relativePath, StringComparison.OrdinalIgnoreCase));
                var hasCollision = existingDocument is not null
                    || await _fileService.FileExistsAsync(root, relativePath, cancellationToken);
                var collisionDecision = ProjectUploadCollisionPolicy.Cancel;
                if (hasCollision)
                {
                    var existingPath = await _fileService.ResolvePathAsync(root, relativePath, cancellationToken);
                    collisionDecision = await resolveCollision(
                        new ProjectFileCollisionInfo(fileName, relativePath, existingPath),
                        cancellationToken);
                    if (collisionDecision == ProjectUploadCollisionPolicy.Cancel)
                    {
                        skipped.Add(sourceInfo.Name);
                        Report(progress, "Unggah", 10 + (index * 80 / total), $"{sourceInfo.Name} dilewati.");
                        continue;
                    }

                    var fileStillExists = await _fileService.FileExistsAsync(root, relativePath, cancellationToken);
                    if (collisionDecision == ProjectUploadCollisionPolicy.SaveAsDifferentName
                        || (collisionDecision == ProjectUploadCollisionPolicy.ReplaceWithBackup && !fileStillExists))
                    {
                        var uniquePath = await _fileService.EnsureUniquePathAsync(root, relativePath, cancellationToken);
                        relativePath = await _fileService.ToRelativePathAsync(root, uniquePath, cancellationToken);
                    }
                }

                if (await _fileService.FileExistsAsync(root, relativePath, cancellationToken))
                {
                    await _fileService.CopyWithBackupAsync(sourcePath, root, relativePath, cancellationToken);
                }
                else
                {
                    await _fileService.CopyAsync(sourcePath, root, relativePath, false, cancellationToken);
                }

                createdPaths.Add(relativePath);
                var metadata = await _fileService.GetFileMetadataAsync(root, relativePath, cancellationToken);
                var size = metadata?.Length ?? sourceInfo.Length;
                var modified = metadata?.LastWriteTimeUtc ?? new DateTimeOffset(sourceInfo.LastWriteTimeUtc);
                ProjectDocumentEntity entity;
                if (pendingRequirement is not null)
                {
                    entity = await _dbContext.ProjectDocuments.FirstAsync(
                        item => item.Id == pendingRequirement.Id,
                        cancellationToken);
                    entity.FilePath = relativePath;
                    entity.FileType = Path.GetExtension(relativePath);
                    entity.Status = DocumentStatuses.Incomplete;
                    entity.VersionSnapshot = "manual";
                    entity.FileSizeBytes = size;
                    entity.LastModifiedUtc = modified;
                    entity.UpdatedAt = now;
                    documents.Add(Map(entity));
                }
                else if (existingDocument is not null && collisionDecision == ProjectUploadCollisionPolicy.ReplaceWithBackup)
                {
                    entity = await _dbContext.ProjectDocuments.FirstAsync(
                        item => item.Id == existingDocument.Id,
                        cancellationToken);
                    entity.FilePath = relativePath;
                    entity.FileType = Path.GetExtension(relativePath);
                    entity.DisplayName = sourceInfo.Name;
                    entity.Source = DocumentSources.ManualUpload;
                    entity.Status = DocumentStatuses.Incomplete;
                    entity.VersionSnapshot = "manual";
                    entity.MasterDocumentId = null;
                    entity.MasterVersionId = null;
                    entity.IsRequired = false;
                    entity.FileSizeBytes = size;
                    entity.LastModifiedUtc = modified;
                    entity.UpdatedAt = now;
                    documents.Add(Map(entity));
                }
                else
                {
                    entity = new ProjectDocumentEntity
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProjectCategoryId = projectCategoryId,
                        MasterDocumentId = null,
                        MasterVersionId = null,
                        DisplayName = sourceInfo.Name,
                        FilePath = relativePath,
                        FileType = Path.GetExtension(relativePath),
                        Source = DocumentSources.ManualUpload,
                        Status = DocumentStatuses.Incomplete,
                        VersionSnapshot = "manual",
                        IsRequired = false,
                        FileSizeBytes = size,
                        LastModifiedUtc = modified,
                        ImportedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.ProjectDocuments.Add(entity);
                    documents.Add(Map(entity));
                }

                knownDocuments.RemoveAll(item => item.Id == entity.Id);
                knownDocuments.Add(new ProjectDocumentEntity
                {
                    Id = entity.Id,
                    ProjectId = projectId,
                    ProjectCategoryId = projectCategoryId,
                    FilePath = relativePath,
                    DisplayName = entity.DisplayName
                });
                Report(progress, "Unggah", 10 + ((index + 1) * 80 / total), $"{sourceInfo.Name} tersalin.");
            }

            Report(progress, "Database", 95, "Menyimpan metadata unggahan.");
            await _dbContext.Projects
                .Where(item => item.Id == projectId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAt, now), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            foreach (var createdPath in createdPaths)
            {
                await TryDeleteFileAsync(root, createdPath);
            }

            throw;
        }

        Report(progress, "Selesai", 100, $"{documents.Count} file berhasil diunggah.");
        var message = skipped.Count == 0
            ? $"{documents.Count} file berhasil diunggah."
            : $"{documents.Count} file berhasil diunggah, {skipped.Count} file dilewati.";
        return new ProjectUploadResult(documents, skipped, message);
    }

    public async Task<ProjectRefreshResult> RefreshFilesystemAsync(
        Guid projectId,
        IProgress<ProjectCreationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var detail = await BuildDetailAsync(projectId, cancellationToken);
        if (detail is null)
        {
            throw new InvalidOperationException("Pekerjaan tidak ditemukan.");
        }

        var root = PathSafety.NormalizeRoot(detail.Project.RootPath);
        var knownDocuments = detail.Documents
            .Where(document => !string.IsNullOrWhiteSpace(document.FilePath))
            .ToDictionary(
                document => document.FilePath,
                document => document,
                StringComparer.OrdinalIgnoreCase);
        var changes = new List<ProjectRefreshChange>();
        var now = DateTimeOffset.UtcNow;
        var touched = false;

        Report(progress, "Pindai", 10, "Membaca folder pekerjaan.");
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            for (var index = 0; index < detail.Categories.Count; index++)
            {
                var category = detail.Categories[index];
                var files = await _fileService.ListFilesAsync(root, category.FolderPath, false, cancellationToken);
                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    if (Path.GetFileName(file.RelativePath).Contains(".bak-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    present.Add(file.RelativePath);
                    if (knownDocuments.TryGetValue(file.RelativePath, out var existing))
                    {
                        var entity = await _dbContext.ProjectDocuments
                            .FirstAsync(document => document.Id == existing.Id, cancellationToken);
                        var restored = entity.Status == DocumentStatuses.NotAvailable;
                        var metadataChanged = entity.FileSizeBytes != file.Length
                            || entity.LastModifiedUtc != file.LastWriteTimeUtc;
                        if (restored || metadataChanged)
                        {
                            if (restored)
                            {
                                entity.Status = DocumentStatuses.Incomplete;
                            }

                            if (metadataChanged)
                            {
                                entity.FileSizeBytes = file.Length;
                                entity.LastModifiedUtc = file.LastWriteTimeUtc;
                            }

                            entity.UpdatedAt = now;
                            changes.Add(new ProjectRefreshChange(
                                file.RelativePath,
                                restored ? "Kembali Tersedia" : "Diubah",
                                restored
                                    ? "File kembali tersedia; status dikembalikan ke belum lengkap."
                                    : "Metadata file diperbarui."));
                            touched = true;
                        }

                        continue;
                    }

                    var pendingRequirement = detail.Documents.FirstOrDefault(document =>
                        document.ProjectCategoryId == category.Id
                        && string.Equals(
                            document.Source,
                            DocumentSources.ChecklistItem,
                            StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(document.FilePath)
                        && MatchesDisplayName(document.DisplayName, file.FileName));
                    if (pendingRequirement is not null)
                    {
                        var entity = await _dbContext.ProjectDocuments
                            .FirstAsync(document => document.Id == pendingRequirement.Id, cancellationToken);
                        entity.FilePath = file.RelativePath;
                        entity.FileType = Path.GetExtension(file.RelativePath);
                        entity.Status = DocumentStatuses.Incomplete;
                        entity.VersionSnapshot = "manual";
                        entity.FileSizeBytes = file.Length;
                        entity.LastModifiedUtc = file.LastWriteTimeUtc;
                        entity.UpdatedAt = now;
                        knownDocuments[file.RelativePath] = Map(entity);
                        changes.Add(new ProjectRefreshChange(
                            file.RelativePath,
                            "Item Wajib Terisi",
                            "File dari Explorer diserap ke item checklist wajib."));
                        touched = true;
                        continue;
                    }

                    var added = new ProjectDocumentEntity
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        ProjectCategoryId = category.Id,
                        MasterDocumentId = null,
                        MasterVersionId = null,
                        DisplayName = file.FileName,
                        FilePath = file.RelativePath,
                        FileType = Path.GetExtension(file.RelativePath),
                        Source = DocumentSources.ManualUpload,
                        Status = DocumentStatuses.Incomplete,
                        VersionSnapshot = "manual",
                        IsRequired = false,
                        FileSizeBytes = file.Length,
                        LastModifiedUtc = file.LastWriteTimeUtc,
                        ImportedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.ProjectDocuments.Add(added);
                    changes.Add(new ProjectRefreshChange(
                        file.RelativePath,
                        "Ditambahkan",
                        "File baru dari Explorer dicatat sebagai unggahan manual."));
                    touched = true;
                }

                var missing = detail.Documents.Where(document =>
                    document.ProjectCategoryId == category.Id
                    && !string.IsNullOrWhiteSpace(document.FilePath)
                    && document.Status != DocumentStatuses.NotAvailable
                    && !present.Contains(document.FilePath));
                foreach (var document in missing)
                {
                    var entity = await _dbContext.ProjectDocuments
                        .FirstAsync(item => item.Id == document.Id, cancellationToken);
                    entity.Status = DocumentStatuses.NotAvailable;
                    entity.UpdatedAt = now;
                    changes.Add(new ProjectRefreshChange(
                        document.FilePath,
                        "Tidak Tersedia",
                        "Catatan disimpan dan ditandai belum tersedia."));
                    touched = true;
                }

                Report(
                    progress,
                    "Pindai",
                    10 + ((index + 1) * 80 / Math.Max(1, detail.Categories.Count)),
                    $"Memeriksa {category.Name}.");
            }

            Report(progress, "Database", 95, "Menyimpan hasil rekonsiliasi.");
            if (touched)
            {
                await _dbContext.Projects
                    .Where(item => item.Id == projectId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAt, now), cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var refreshed = await BuildDetailAsync(projectId, cancellationToken);
        if (refreshed is null)
        {
            throw new InvalidOperationException("Pekerjaan tidak ditemukan setelah refresh.");
        }

        Report(progress, "Selesai", 100, $"{changes.Count} perubahan ditemukan.");
        var message = changes.Count == 0
            ? "Filesystem sudah sinkron dengan metadata pekerjaan."
            : $"{changes.Count} perubahan berhasil direkonsiliasi.";
        return new ProjectRefreshResult(refreshed, changes, message);
    }

    public async Task<string> ResolveProjectPathAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectEntityAsync(projectId, cancellationToken);
        return PathSafety.NormalizeRoot(project.RootPath);
    }

    public async Task<string> ResolveCategoryPathAsync(
        Guid projectCategoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.ProjectCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectCategoryId, cancellationToken);
        if (category is null)
        {
            throw new InvalidOperationException("Kategori pekerjaan tidak ditemukan.");
        }

        var root = await ResolveProjectPathAsync(category.ProjectId, cancellationToken);
        return await _fileService.ResolvePathAsync(root, category.FolderPath, cancellationToken);
    }

    public async Task<string> ResolveDocumentPathAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("Dokumen pekerjaan tidak ditemukan.");
        }

        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            throw new InvalidOperationException("Dokumen ini belum memiliki file.");
        }

        var root = await ResolveProjectPathAsync(document.ProjectId, cancellationToken);
        return await _fileService.ResolvePathAsync(root, document.FilePath, cancellationToken);
    }

    public async Task<ProjectDocumentInfo> SetDocumentStatusAsync(
        Guid documentId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var allowed = new HashSet<string>(
            new[]
            {
                DocumentStatuses.NotAvailable,
                DocumentStatuses.Incomplete,
                DocumentStatuses.InProgress,
                DocumentStatuses.Completed
            },
            StringComparer.Ordinal);
        if (!allowed.Contains(status))
        {
            throw new ArgumentException("Status dokumen tidak dikenal.", nameof(status));
        }

        var document = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("Dokumen pekerjaan tidak ditemukan.");
        }

        var hasFile = false;
        if (!string.IsNullOrWhiteSpace(document.FilePath))
        {
            try
            {
                var path = await ResolveDocumentPathAsync(documentId, cancellationToken);
                hasFile = File.Exists(path);
            }
            catch (InvalidOperationException)
            {
                hasFile = false;
            }
        }

        if (!hasFile && !string.Equals(status, DocumentStatuses.NotAvailable, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Dokumen belum memiliki file; status hanya dapat ditandai Belum Tersedia.");
        }

        if (hasFile && string.Equals(status, DocumentStatuses.NotAvailable, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Dokumen sudah memiliki file; status tidak dapat ditandai Belum Tersedia.");
        }

        var now = DateTimeOffset.UtcNow;
        document.Status = status;
        document.UpdatedAt = now;
        await _dbContext.Projects
            .Where(item => item.Id == document.ProjectId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAt, now), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(document);
    }

    public void Dispose()
    {
        foreach (var service in _ownedServices)
        {
            service.Dispose();
        }

        _dbContext.Dispose();
    }

    private async Task<ProjectEntity> GetProjectEntityAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException("Pekerjaan tidak ditemukan.");
        }

        return project;
    }

    private async Task<ProjectDetailInfo?> BuildDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var categories = await _dbContext.ProjectCategories
            .AsNoTracking()
            .Where(category => category.ProjectId == projectId)
            .OrderBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);
        var documents = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .Where(document => document.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var fields = await _dbContext.ProjectFields
            .AsNoTracking()
            .Where(field => field.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        var orderedDocuments = documents
            .OrderBy(document => document.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(Map)
            .ToArray();

        return new ProjectDetailInfo(
            Map(project),
            categories.Select(Map).ToArray(),
            orderedDocuments,
            fields
                .OrderBy(field => field.Key, StringComparer.OrdinalIgnoreCase)
                .Select(Map)
                .ToArray(),
            BuildChecklist(orderedDocuments));
    }

    private static ProjectChecklistSummary BuildChecklist(IReadOnlyList<ProjectDocumentInfo> documents)
    {
        var required = documents.Where(document => document.IsRequired).ToArray();
        var completedRequired = required.Count(document => document.Status == DocumentStatuses.Completed);
        return new ProjectChecklistSummary(
            required.Length,
            documents.Count(document => document.Status == DocumentStatuses.NotAvailable),
            documents.Count(document => document.Status == DocumentStatuses.Incomplete),
            documents.Count(document => document.Status == DocumentStatuses.InProgress),
            documents.Count(document => document.Status == DocumentStatuses.Completed),
            completedRequired,
            required.Length == 0 ? 0 : completedRequired * 100 / required.Length);
    }

    private static void ValidateUploadSource(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("File sumber tidak ditemukan.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath);
        if (!AllowedUploadExtensions.Contains(extension))
        {
            throw new ArgumentException($"Format file tidak didukung: {extension}", nameof(sourcePath));
        }
    }

    private async Task TryDeleteFileAsync(string root, string relativePath)
    {
        try
        {
            await _fileService.DeleteFileAsync(root, relativePath);
        }
        catch (Exception exception)
        {
            _logger.Warning($"Gagal membersihkan file unggahan yang dibatalkan: {relativePath}. {exception.Message}");
        }
    }

    private async Task<string> CopyMasterAsync(
        MasterDocumentInfo master,
        string stagingPath,
        string categoryFolder,
        CancellationToken cancellationToken)
    {
        var sourcePath = await _masterStore.ResolveOpenPathAsync(master.Id, cancellationToken);
        var physicalName = PathSafety.SanitizeName(
            string.IsNullOrWhiteSpace(master.SourceFileName)
                ? $"{master.DisplayName}{master.FileType}"
                : Path.GetFileName(master.SourceFileName),
            '_',
            120,
            false);
        var relativePath = Path.Combine(categoryFolder, physicalName);
        var uniqueAbsolute = await _fileService.EnsureUniquePathAsync(stagingPath, relativePath, cancellationToken);
        var uniqueRelative = Path.GetRelativePath(stagingPath, uniqueAbsolute);
        var copied = await _fileService.CopyAsync(sourcePath, stagingPath, uniqueRelative, false, cancellationToken);
        var checksum = await _fileService.ComputeChecksumAsync(copied, cancellationToken);
        if (!string.Equals(checksum, master.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException($"Checksum salinan '{master.DisplayName}' tidak cocok dengan master.");
        }

        return uniqueRelative;
    }

    private async Task<ProjectDuplicateInfo?> FindDuplicateCoreAsync(
        string shipName,
        string jobName,
        string finalPath,
        CancellationToken cancellationToken)
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var duplicate = projects.FirstOrDefault(project =>
            string.Equals(project.RootPath, finalPath, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(project.ShipName, shipName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(project.JobName, jobName, StringComparison.OrdinalIgnoreCase)));
        if (duplicate is not null)
        {
            return new ProjectDuplicateInfo(duplicate.Id, duplicate.ShipName, duplicate.JobName, duplicate.RootPath);
        }

        if (Directory.Exists(finalPath) || File.Exists(finalPath))
        {
            return new ProjectDuplicateInfo(null, shipName, jobName, finalPath);
        }

        return null;
    }

    private static List<ProjectFieldEntity> BuildFieldEntities(
        Guid projectId,
        string shipName,
        string jobName,
        IReadOnlyList<ProjectFieldRequest> fields)
    {
        var entries = new List<ProjectFieldRequest>(fields ?? Array.Empty<ProjectFieldRequest>());
        Upsert(entries, ProfileFieldKeys.ShipName, shipName);
        Upsert(entries, ProfileFieldKeys.JobName, jobName);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entities = new List<ProjectFieldEntity>();
        foreach (var field in entries)
        {
            var key = (field.Key ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Kunci field profil tidak boleh kosong.", nameof(fields));
            }

            if (!seen.Add(key))
            {
                throw new ArgumentException($"Kunci field profil duplikat: {key}", nameof(fields));
            }

            entities.Add(new ProjectFieldEntity
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Key = key,
                Value = field.Value ?? string.Empty,
                FieldType = string.IsNullOrWhiteSpace(field.FieldType) ? "Text" : field.FieldType.Trim(),
                Source = FieldSources.Profile
            });
        }

        return entities;
    }

    private static void Upsert(List<ProjectFieldRequest> entries, string key, string value)
    {
        if (entries.Any(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        entries.Add(new ProjectFieldRequest(key, value, "Text"));
    }

    private static string ValidateName(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} tidak boleh kosong.", nameof(value));
        }

        return value.Trim();
    }

    private static string BuildVersionSnapshot(string version, string? checksum) =>
        string.IsNullOrWhiteSpace(checksum) ? version : $"{version}:{checksum[..Math.Min(12, checksum.Length)]}";

    private static void Report(
        IProgress<ProjectCreationProgressInfo>? progress,
        string stage,
        int percent,
        string message) =>
        progress?.Report(new ProjectCreationProgressInfo(stage, percent, message));

    private async Task TryCleanupStagingAsync(string stagingPath, string projectsRoot)
    {
        try
        {
            await _fileService.CleanupStagingAsync(stagingPath, projectsRoot);
        }
        catch (Exception exception)
        {
            _logger.Warning($"Gagal membersihkan staging pekerjaan: {stagingPath}. {exception.Message}");
        }
    }

    private void TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }

        _logger.Warning($"Gagal menghapus folder pekerjaan hasil generate yang dibatalkan: {path}");
    }

    private static bool MatchesDisplayName(string displayName, string fileName)
    {
        var normalized = PathSafety.SanitizeName(displayName, '_', 120, false);
        return string.Equals(normalized, fileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectInfo Map(ProjectEntity entity) =>
        new(
            entity.Id,
            entity.ShipName,
            entity.JobName,
            entity.RootPath,
            entity.Status,
            entity.StructureVersion,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static ProjectCategoryInfo Map(ProjectCategoryEntity entity) =>
        new(
            entity.Id,
            entity.ProjectId,
            entity.SourceCategoryId,
            entity.Name,
            entity.SortOrder,
            entity.FolderPath);

    private static ProjectDocumentInfo Map(ProjectDocumentEntity entity) =>
        new(
            entity.Id,
            entity.ProjectId,
            entity.ProjectCategoryId,
            entity.MasterDocumentId,
            entity.MasterVersionId,
            entity.DisplayName,
            entity.FilePath,
            entity.FileType,
            entity.Source,
        entity.Status,
        entity.VersionSnapshot,
        entity.IsRequired,
        entity.FileSizeBytes,
        entity.LastModifiedUtc,
        entity.ImportedAt,
        entity.UpdatedAt,
        entity.BaseTemplateFilePath ?? string.Empty,
        entity.EditedOutputFilePath ?? string.Empty,
        entity.LastEditedAt);

    private static ProjectFieldInfo Map(ProjectFieldEntity entity) =>
        new(
            entity.Id,
            entity.ProjectId,
            entity.Key,
            entity.Value,
            entity.FieldType,
            entity.Source);
}
