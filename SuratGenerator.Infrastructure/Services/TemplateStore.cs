using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class TemplateStore : ITemplateStore, IDisposable
{
    private readonly SuratGeneratorDbContext _dbContext;
    private readonly string _templatesDirectory;
    private readonly IAppLogger _logger;

    public TemplateStore(IAppLogger? logger = null)
        : this(DatabaseOptionsFactory.CreateProduction(), AppPaths.TemplatesDirectory, logger)
    {
    }

    public TemplateStore(
        DbContextOptions<SuratGeneratorDbContext> options,
        string templatesDirectory,
        IAppLogger? logger = null)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        _templatesDirectory = templatesDirectory;
        _logger = logger ?? NullAppLogger.Instance;
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<TemplateInfo> SaveAsync(
        string sourceFilePath,
        IReadOnlyList<TemplateField> fields,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("File template tidak ditemukan.", sourceFilePath);
        }

        Directory.CreateDirectory(_templatesDirectory);

        var id = Guid.NewGuid();
        var extension = Path.GetExtension(sourceFilePath);
        var storedFilePath = Path.Combine(_templatesDirectory, $"{id:N}{extension}");
        File.Copy(sourceFilePath, storedFilePath, false);

        var importedAt = DateTimeOffset.UtcNow;
        var info = new TemplateInfo(
            id,
            Path.GetFileNameWithoutExtension(sourceFilePath),
            Path.GetFileName(sourceFilePath),
            storedFilePath,
            new FileInfo(storedFilePath).Length,
            importedAt,
            fields.ToArray());

        try
        {
            var entity = new TemplateEntity
            {
                Id = id,
                Name = info.Name,
                SourceFileName = info.SourceFileName,
                StoredFilePath = storedFilePath,
                FileSizeBytes = info.FileSizeBytes,
                ImportedAt = importedAt,
                Category = info.Category,
                IsFavorite = info.IsFavorite,
                Version = info.Version,
                Fields = fields.Select(field => new TemplateFieldEntity
                {
                    TemplateId = id,
                    Key = field.Key,
                    Label = field.Label,
                    Position = field.Position,
                    Kind = field.Kind.ToString()
                }).ToList()
            };

            _dbContext.Templates.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return info;
        }
        catch (Exception ex)
        {
            _logger.Error("Gagal menyimpan template ke database.", ex);
            if (File.Exists(storedFilePath))
            {
                File.Delete(storedFilePath);
            }

            throw;
        }
    }

    public async Task<TemplateInfo?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Templates
            .AsNoTracking()
            .Include(template => template.Fields)
            .FirstOrDefaultAsync(template => template.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<TemplateInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Templates
            .AsNoTracking()
            .Include(template => template.Fields)
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(template => template.ImportedAt)
            .Select(Map)
            .ToArray();
    }

    public async Task<TemplateInfo> ReplaceFileAsync(
        Guid id,
        string sourceFilePath,
        IReadOnlyList<TemplateField> fields,
        string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("File template tidak ditemukan.", sourceFilePath);
        }

        var entity = await _dbContext.Templates
            .Include(template => template.Fields)
            .FirstOrDefaultAsync(template => template.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new InvalidOperationException("Template lokal tidak ditemukan.");
        }

        File.Copy(sourceFilePath, entity.StoredFilePath, true);
        entity.SourceFileName = string.IsNullOrWhiteSpace(sourceFileName)
            ? Path.GetFileName(sourceFilePath)
            : sourceFileName;
        entity.FileSizeBytes = new FileInfo(entity.StoredFilePath).Length;
        entity.Fields.Clear();
        entity.Fields.AddRange(fields.Select(field => new TemplateFieldEntity
        {
            TemplateId = id,
            Key = field.Key,
            Label = field.Label,
            Position = field.Position,
            Kind = field.Kind.ToString()
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<TemplateInfo?> GetBySourcePathAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(sourceFilePath);
        var entity = await _dbContext.Templates
            .AsNoTracking()
            .Include(template => template.Fields)
            .FirstOrDefaultAsync(
                template => template.SourceFilePath == normalized,
                cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<TemplateInfo> SetSourceAsync(
        Guid id,
        string sourceFilePath,
        string checksum,
        DateTimeOffset modifiedAt,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Templates
            .Include(template => template.Fields)
            .FirstOrDefaultAsync(template => template.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new InvalidOperationException("Template lokal tidak ditemukan.");
        }

        var normalized = Path.GetFullPath(sourceFilePath);
        entity.SourceFilePath = normalized;
        entity.SourceFileName = Path.GetFileName(normalized);
        entity.SourceChecksum = checksum;
        entity.SourceModifiedAt = modifiedAt;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task UpdateMetadataAsync(
        Guid id,
        string name,
        string category,
        string version,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Templates.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Name = string.IsNullOrWhiteSpace(name) ? entity.Name : name.Trim();
        entity.Category = string.IsNullOrWhiteSpace(category) ? "Umum" : category.Trim();
        entity.Version = string.IsNullOrWhiteSpace(version) ? "1.0" : version.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetFavoriteAsync(
        Guid id,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Templates.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.IsFavorite = isFavorite;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Templates
            .Include(template => template.Fields)
            .FirstOrDefaultAsync(template => template.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var drafts = await _dbContext.Drafts
            .Where(draft => draft.TemplateId == id)
            .ToListAsync(cancellationToken);

        var syncState = await _dbContext.TemplateSyncStates.FindAsync([id], cancellationToken);
        if (syncState is null)
        {
            syncState = new TemplateSyncStateEntity { TemplateId = id };
            _dbContext.TemplateSyncStates.Add(syncState);
        }

        syncState.IsLocallyDeleted = true;
        syncState.Status = "Dihapus lokal";

        _dbContext.Drafts.RemoveRange(drafts);
        _dbContext.Templates.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        TryDeleteFile(entity.StoredFilePath);
        return true;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static TemplateInfo Map(TemplateEntity entity)
    {
        return new TemplateInfo(
            entity.Id,
            entity.Name,
            entity.SourceFileName,
            entity.StoredFilePath,
            entity.FileSizeBytes,
            entity.ImportedAt,
            entity.Fields
                .OrderBy(field => field.Position)
                .Select(field => new TemplateField(
                    field.Key,
                    field.Label,
                    field.Position,
                    Enum.Parse<TemplateFieldKind>(field.Kind)))
                .ToArray(),
            entity.Category,
            entity.IsFavorite,
            entity.Version,
            entity.SourceFilePath,
            entity.SourceChecksum,
            entity.SourceModifiedAt);
    }

    private void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException ex)
            {
                if (attempt == 2)
                {
                    _logger.Warning($"Gagal menghapus file template '{path}' setelah tiga percobaan.");
                    return;
                }

                _logger.Warning($"Gagal menghapus file template '{path}', mencoba lagi: {ex.Message}");
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Warning($"Tidak diizinkan menghapus file template '{path}': {ex.Message}");
                return;
            }
        }
    }
}
