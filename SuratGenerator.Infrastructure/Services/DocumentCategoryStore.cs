using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class DocumentCategoryStore : IDocumentCategoryStore, IDisposable
{
    private const string SeedMarker = "seed.document-categories.v1";
    private const int MaximumNameLength = 80;

    private static readonly (Guid Id, string Name)[] DefaultCategories =
    {
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0001"), "Dokumen Tagihan"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0002"), "Efaktur"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0003"), "Undangan Verifikasi"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0004"), "SPMK"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0005"), "JAMLAK"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0006"), "JAMHAR"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0007"), "Pengumuman Tender"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0008"), "Penunjukan Pemenang"),
        (Guid.Parse("8f0f6a11-3c8e-4f2a-9a41-2d0f8e6c0009"), "Format Verifikasi")
    };

    private readonly SuratGeneratorDbContext _dbContext;
    private readonly IAppLogger _logger;

    public DocumentCategoryStore(IAppLogger? logger = null)
        : this(DatabaseOptionsFactory.CreateProduction(), logger)
    {
    }

    public DocumentCategoryStore(
        DbContextOptions<SuratGeneratorDbContext> options,
        IAppLogger? logger = null)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        _logger = logger ?? NullAppLogger.Instance;
        DatabaseInitializer.Initialize(_dbContext);
        EnsureSeeded();
    }

    public async Task<IReadOnlyList<DocumentCategoryInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.DocumentCategories
            .AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task<MasterStructureSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await GetAllAsync(cancellationToken);
        var fingerprint = string.Join(
            "|",
            categories.Select(category =>
                $"{category.Id:N}:{category.SortOrder}:{category.Version}:{(category.IsActive ? 1 : 0)}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))
            .ToLowerInvariant();
        var version = $"str-{hash[..12]}";

        return new MasterStructureSnapshot(
            version,
            DateTimeOffset.UtcNow,
            categories.Where(category => category.IsActive).ToArray());
    }

    public async Task<DocumentCategoryInfo> AddAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var text = ValidateName(name);
        var normalized = NormalizeName(text);
        await EnsureUniqueNameAsync(normalized, null, cancellationToken);

        var maxSortOrder = await _dbContext.DocumentCategories
            .MaxAsync(category => (int?)category.SortOrder, cancellationToken) ?? 0;
        var entity = new DocumentCategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = text,
            NormalizedName = normalized,
            SortOrder = maxSortOrder + 1,
            IsActive = true,
            Version = "1.0",
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.DocumentCategories.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<DocumentCategoryInfo> RenameAsync(
        Guid id,
        string name,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        var text = ValidateName(name);
        var normalized = NormalizeName(text);
        await EnsureUniqueNameAsync(normalized, id, cancellationToken);

        entity.Name = text;
        entity.NormalizedName = normalized;
        entity.Version = NextVersion(entity.Version);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await FindEntityAsync(id, cancellationToken);
            var checklistItems = await _dbContext.CategoryChecklistItems
                .Where(item => item.CategoryId == id)
                .ToListAsync(cancellationToken);
            _dbContext.CategoryChecklistItems.RemoveRange(checklistItems);

            var projectCategories = await _dbContext.ProjectCategories
                .Where(category => category.SourceCategoryId == id)
                .ToListAsync(cancellationToken);
            foreach (var projectCategory in projectCategories)
            {
                projectCategory.SourceCategoryId = null;
            }

            _dbContext.DocumentCategories.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var remaining = await _dbContext.DocumentCategories
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Name)
                .ToListAsync(cancellationToken);
            for (var index = 0; index < remaining.Count; index++)
            {
                remaining[index].SortOrder = index + 1;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task ReorderAsync(
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default)
    {
        var ids = orderedIds.Distinct().ToArray();
        if (ids.Length != orderedIds.Count)
        {
            throw new ArgumentException("Urutan kategori mengandung ID duplikat.", nameof(orderedIds));
        }

        var entities = await _dbContext.DocumentCategories.ToListAsync(cancellationToken);
        if (entities.Count != ids.Length || entities.Any(entity => !ids.Contains(entity.Id)))
        {
            throw new ArgumentException("Urutan kategori harus memuat seluruh kategori.", nameof(orderedIds));
        }

        var byId = entities.ToDictionary(entity => entity.Id);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < orderedIds.Count; index++)
        {
            var entity = byId[orderedIds[index]];
            entity.SortOrder = index + 1;
            entity.Version = NextVersion(entity.Version);
            entity.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentCategoryInfo> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        entity.IsActive = isActive;
        entity.Version = NextVersion(entity.Version);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private void EnsureSeeded()
    {
        if (_dbContext.SchemaVersions.Any(version => version.Version == SeedMarker))
        {
            return;
        }

        using var transaction = _dbContext.Database.BeginTransaction();
        try
        {
            if (_dbContext.SchemaVersions.Any(version => version.Version == SeedMarker))
            {
                transaction.Commit();
                return;
            }

            if (!_dbContext.DocumentCategories.Any())
            {
                var now = DateTimeOffset.UtcNow;
                _dbContext.DocumentCategories.AddRange(DefaultCategories.Select((item, index) =>
                    new DocumentCategoryEntity
                    {
                        Id = item.Id,
                        Name = item.Name,
                        NormalizedName = NormalizeName(item.Name),
                        SortOrder = index + 1,
                        IsActive = true,
                        Version = "1.0",
                        UpdatedAt = now
                    }));
                _dbContext.SaveChanges();
            }

            _dbContext.SchemaVersions.Add(new SchemaVersionEntity
            {
                Version = SeedMarker,
                AppliedAt = DateTimeOffset.UtcNow
            });
            _dbContext.SaveChanges();
            transaction.Commit();
        }
        catch (Exception exception)
        {
            _logger.Error("Gagal menyiapkan seed kategori dokumen.", exception);
            transaction.Rollback();
            throw;
        }
    }

    private async Task<DocumentCategoryEntity> FindEntityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.DocumentCategories
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

        return entity ?? throw new InvalidOperationException("Kategori dokumen tidak ditemukan.");
    }

    private async Task EnsureUniqueNameAsync(
        string normalizedName,
        Guid? ignoreId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.DocumentCategories.AsNoTracking().AsQueryable();
        if (ignoreId.HasValue)
        {
            query = query.Where(category => category.Id != ignoreId.Value);
        }

        var names = await query
            .Select(category => category.NormalizedName)
            .ToListAsync(cancellationToken);

        if (names.Any(name => string.Equals(name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Nama kategori sudah digunakan.", nameof(normalizedName));
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nama kategori tidak boleh kosong.", nameof(name));
        }

        if (PathSafety.HasTrailingDotOrSpace(name))
        {
            throw new ArgumentException("Nama kategori tidak boleh diakhiri titik atau spasi.", nameof(name));
        }

        var text = name.Trim();
        if (text.Length > MaximumNameLength)
        {
            throw new ArgumentException($"Nama kategori maksimal {MaximumNameLength} karakter.", nameof(name));
        }

        if (PathSafety.HasInvalidFileNameChars(text))
        {
            throw new ArgumentException("Nama kategori mengandung karakter yang tidak diizinkan Windows.", nameof(name));
        }

        if (PathSafety.IsReservedName(text))
        {
            throw new ArgumentException("Nama kategori termasuk reserved name Windows.", nameof(name));
        }

        return text;
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private static string NextVersion(string current)
    {
        var parts = current.Split('.');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var major)
            && int.TryParse(parts[1], out var minor))
        {
            return $"{major}.{minor + 1}";
        }

        return "1.1";
    }

    private static DocumentCategoryInfo Map(DocumentCategoryEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.SortOrder,
            entity.IsActive,
            entity.Version,
            entity.UpdatedAt);
}
