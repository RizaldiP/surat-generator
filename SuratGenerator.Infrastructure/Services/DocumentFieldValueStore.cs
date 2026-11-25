using Microsoft.EntityFrameworkCore;
using SuratGenerator.Documents.Services;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class DocumentFieldValueStore : IDocumentFieldValueStore, IDisposable
{
    private readonly SuratGeneratorDbContext _dbContext;
    private readonly IWorkspaceFileService _fileService;
    private readonly IPlaceholderAliasService _aliasService;
    private readonly ITemplateReader _templateReader;

    public DocumentFieldValueStore(IAppLogger? logger = null)
        : this(
            DatabaseOptionsFactory.CreateProduction(),
            new WorkspaceFileService(),
            new PlaceholderAliasService(),
            new TemplateReader(),
            logger)
    {
    }

    public DocumentFieldValueStore(
        DbContextOptions<SuratGeneratorDbContext> options,
        IWorkspaceFileService? fileService = null,
        IPlaceholderAliasService? aliasService = null,
        ITemplateReader? templateReader = null,
        IAppLogger? logger = null)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        _fileService = fileService ?? new WorkspaceFileService();
        _aliasService = aliasService ?? new PlaceholderAliasService();
        _templateReader = templateReader ?? new TemplateReader();
        _ = logger ?? NullAppLogger.Instance;
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<DocumentFieldEditInfo> EnsureEditSessionAsync(
        Guid projectDocumentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(item => item.Id == projectDocumentId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("Dokumen pekerjaan tidak ditemukan.");
        }

        if (!string.Equals(Path.GetExtension(document.FilePath), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Hanya dokumen Word .docx yang dapat diisi melalui editor.");
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == document.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException("Pekerjaan tidak ditemukan.");
        }

        var root = PathSafety.NormalizeRoot(project.RootPath);
        var currentPath = await _fileService.ResolvePathAsync(root, document.FilePath, cancellationToken);
        if (!File.Exists(currentPath))
        {
            throw new FileNotFoundException("File dokumen tidak ditemukan di folder pekerjaan.", currentPath);
        }

        var basePath = await ResolveStoredPathAsync(root, document.BaseTemplateFilePath, cancellationToken);
        if (basePath is null || !File.Exists(basePath))
        {
            var directory = Path.GetDirectoryName(currentPath)
                ?? throw new InvalidOperationException("Lokasi dokumen tidak valid.");
            basePath = Path.Combine(directory, $".{document.Id:N}.base.docx");
            if (!File.Exists(basePath))
            {
                File.Copy(currentPath, basePath, false);
            }

            document.BaseTemplateFilePath = await _fileService.ToRelativePathAsync(root, basePath, cancellationToken);
        }

        var outputPath = await ResolveStoredPathAsync(root, document.EditedOutputFilePath, cancellationToken);
        if (outputPath is null)
        {
            var directory = Path.GetDirectoryName(basePath)
                ?? throw new InvalidOperationException("Lokasi template dasar tidak valid.");
            var safeName = PathSafety.SanitizeName(
                Path.GetFileNameWithoutExtension(document.DisplayName),
                '_',
                100,
                false);
            outputPath = Path.Combine(directory, $"{safeName} - Hasil Edit.docx");
            document.EditedOutputFilePath = await _fileService.ToRelativePathAsync(root, outputPath, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var values = (await _dbContext.DocumentFieldValues
                .AsNoTracking()
                .Where(item => item.ProjectDocumentId == projectDocumentId)
                .Select(item => new DocumentFieldValueInfo(
                    item.Id,
                    item.ProjectDocumentId,
                    item.Key,
                    item.Value,
                    item.IsOverride,
                    item.UpdatedAt))
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DocumentFieldEditInfo(
            document.Id,
            document.ProjectId,
            document.DisplayName,
            basePath,
            outputPath,
            values,
            document.LastEditedAt);
    }

    public async Task<IReadOnlyList<PlaceholderMappingInfo>> GetMappingsAsync(
        CancellationToken cancellationToken = default)
    {
        return (await _dbContext.PlaceholderMappings
                .AsNoTracking()
                .Where(item => item.IsActive)
                .Select(item => new PlaceholderMappingInfo(
                    item.Id,
                    item.PlaceholderKey,
                    item.ProfileFieldKey,
                    item.IsActive,
                    item.UpdatedAt))
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.PlaceholderKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string> PrepareOutputAsync(
        Guid projectDocumentId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var root = await ResolveDocumentRootAsync(projectDocumentId, cancellationToken);
        var resolved = Path.GetFullPath(outputPath);
        if (!PathSafety.IsWithin(root, resolved, false))
        {
            throw new InvalidOperationException("File hasil harus disimpan di dalam folder pekerjaan.");
        }

        if (!string.Equals(Path.GetExtension(resolved), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("File hasil harus berformat .docx.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
        if (File.Exists(resolved))
        {
            var directory = Path.GetDirectoryName(resolved)!;
            var name = Path.GetFileNameWithoutExtension(resolved);
            var backup = Path.Combine(directory, $"{name}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}.docx");
            var suffix = 1;
            while (File.Exists(backup))
            {
                backup = Path.Combine(directory, $"{name}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}-{suffix}.docx");
                suffix++;
            }

            File.Copy(resolved, backup, false);
        }

        return resolved;
    }

    public async Task SaveAsync(
        DocumentFieldEditSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var root = await ResolveDocumentRootAsync(request.ProjectDocumentId, cancellationToken);
        var outputPath = Path.GetFullPath(request.OutputPath);
        if (!PathSafety.IsWithin(root, outputPath, false) || !File.Exists(outputPath))
        {
            throw new InvalidOperationException("File hasil tidak ditemukan di folder pekerjaan.");
        }

        var document = await _dbContext.ProjectDocuments
            .FirstOrDefaultAsync(item => item.Id == request.ProjectDocumentId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("Dokumen pekerjaan tidak ditemukan.");
        }

        var relativeOutputPath = await _fileService.ToRelativePathAsync(root, outputPath, cancellationToken);
        var metadata = await _fileService.GetFileMetadataAsync(root, relativeOutputPath, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        document.EditedOutputFilePath = relativeOutputPath;
        document.LastEditedAt = now;
        document.Status = DocumentStatuses.InProgress;
        document.FileSizeBytes = metadata?.Length ?? new FileInfo(outputPath).Length;
        document.LastModifiedUtc = metadata?.LastWriteTimeUtc ?? new DateTimeOffset(new FileInfo(outputPath).LastWriteTimeUtc);
        document.UpdatedAt = now;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in request.Values)
        {
            var key = _aliasService.NormalizeKey(value.Key);
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key))
            {
                continue;
            }

            var existing = await _dbContext.DocumentFieldValues
                .FirstOrDefaultAsync(
                    item => item.ProjectDocumentId == request.ProjectDocumentId && item.Key == key,
                    cancellationToken);
            if (existing is null)
            {
                _dbContext.DocumentFieldValues.Add(new DocumentFieldValueEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectDocumentId = request.ProjectDocumentId,
                    Key = key,
                    Value = value.Value ?? string.Empty,
                    IsOverride = value.IsOverride,
                    UpdatedAt = now
                });
            }
            else if (value.IsOverride || !existing.IsOverride)
            {
                existing.Value = value.Value ?? string.Empty;
                existing.IsOverride = value.IsOverride;
                existing.UpdatedAt = now;
            }
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(item => item.Id == document.ProjectId, cancellationToken);
        if (project is not null)
        {
            project.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PromoteToProjectFieldsAsync(
        Guid projectDocumentId,
        IReadOnlyList<ProjectFieldSaveInfo> fields,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectDocumentId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("Dokumen pekerjaan tidak ditemukan.");
        }

        var now = DateTimeOffset.UtcNow;
        var mappings = await GetMappingsAsync(cancellationToken);
        var projectFields = await _dbContext.ProjectFields
            .Where(item => item.ProjectId == document.ProjectId)
            .ToListAsync(cancellationToken);
        foreach (var field in fields ?? Array.Empty<ProjectFieldSaveInfo>())
        {
            var normalizedKey = _aliasService.NormalizeKey(field.Key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            var canonicalKey = _aliasService.ResolveProfileFieldKey(field.Key, mappings);
            var existing = projectFields.FirstOrDefault(item =>
                    string.Equals(item.Key, canonicalKey, StringComparison.OrdinalIgnoreCase))
                ?? projectFields.FirstOrDefault(item =>
                    string.Equals(_aliasService.NormalizeKey(item.Key), normalizedKey, StringComparison.Ordinal));
            if (existing is null)
            {
                var created = new ProjectFieldEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = document.ProjectId,
                    Key = canonicalKey,
                    Value = field.Value ?? string.Empty,
                    FieldType = "Text",
                    Source = "Editor Dokumen"
                };
                _dbContext.ProjectFields.Add(created);
                projectFields.Add(created);
            }
            else
            {
                existing.Value = field.Value ?? string.Empty;
                existing.FieldType = "Text";
                existing.Source = "Editor Dokumen";
            }
        }

        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(item => item.Id == document.ProjectId, cancellationToken);
        if (project is not null)
        {
            project.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUnfilledRequiredPlaceholdersAsync(
        Guid projectDocumentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectDocumentId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("Dokumen pekerjaan tidak ditemukan.");
        }

        string basePath;
        if (string.IsNullOrWhiteSpace(document.BaseTemplateFilePath))
        {
            var edit = await EnsureEditSessionAsync(projectDocumentId, cancellationToken);
            basePath = edit.BaseTemplatePath;
        }
        else
        {
            var root = await ResolveDocumentRootAsync(projectDocumentId, cancellationToken);
            basePath = await ResolveStoredPathAsync(root, document.BaseTemplateFilePath, cancellationToken)
                ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(basePath) || !File.Exists(basePath))
        {
            throw new FileNotFoundException("Template dasar dokumen tidak ditemukan.", basePath);
        }

        var read = await _templateReader.ReadAsync(basePath);
        if (!read.IsSuccess)
        {
            throw new InvalidOperationException(read.Error ?? "Template Word tidak dapat dibaca.");
        }

        var savedValues = (await _dbContext.DocumentFieldValues
                .AsNoTracking()
                .Where(item => item.ProjectDocumentId == projectDocumentId)
                .Select(item => new DocumentFieldValueInfo(
                    item.Id,
                    item.ProjectDocumentId,
                    item.Key,
                    item.Value,
                    item.IsOverride,
                    item.UpdatedAt))
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var profileFields = (await _dbContext.ProjectFields
                .AsNoTracking()
                .Where(item => item.ProjectId == document.ProjectId)
                .Select(item => new ProjectFieldInfo(
                    item.Id,
                    item.ProjectId,
                    item.Key,
                    item.Value,
                    item.FieldType,
                    item.Source))
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fields = _aliasService.BuildFields(
            read.Fields,
            profileFields,
            savedValues,
            await GetMappingsAsync(cancellationToken));

        return _aliasService.FindUnfilledPlaceholders(fields);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task<string> ResolveDocumentRootAsync(
        Guid projectDocumentId,
        CancellationToken cancellationToken)
    {
        var document = await _dbContext.ProjectDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectDocumentId, cancellationToken);
        if (document is null)
        {
            throw new InvalidOperationException("Dokumen pekerjaan tidak ditemukan.");
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == document.ProjectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException("Pekerjaan tidak ditemukan.");
        }

        return PathSafety.NormalizeRoot(project.RootPath);
    }

    private async Task<string?> ResolveStoredPathAsync(
        string root,
        string? storedPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        if (Path.IsPathRooted(storedPath))
        {
            var full = Path.GetFullPath(storedPath);
            return PathSafety.IsWithin(root, full, true) ? full : null;
        }

        return await _fileService.ResolvePathAsync(root, storedPath, cancellationToken);
    }
}
