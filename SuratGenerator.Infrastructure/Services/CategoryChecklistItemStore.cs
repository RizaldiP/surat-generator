using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class CategoryChecklistItemStore : ICategoryChecklistItemStore, IDisposable
{
    private const int MaximumNameLength = 100;

    private readonly SuratGeneratorDbContext _dbContext;

    public CategoryChecklistItemStore()
        : this(DatabaseOptionsFactory.CreateProduction())
    {
    }

    public CategoryChecklistItemStore(IAppLogger? logger)
        : this()
    {
        _ = logger ?? NullAppLogger.Instance;
    }

    public CategoryChecklistItemStore(DbContextOptions<SuratGeneratorDbContext> options)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<IReadOnlyList<CategoryChecklistItemInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.CategoryChecklistItems
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<CategoryChecklistItemInfo>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.CategoryChecklistItems
            .AsNoTracking()
            .Where(item => item.CategoryId == categoryId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task<CategoryChecklistItemInfo> AddAsync(
        Guid categoryId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(displayName);
        await EnsureCategoryExistsAsync(categoryId, cancellationToken);
        await EnsureUniqueNameAsync(categoryId, name, null, cancellationToken);

        var maxSortOrder = await _dbContext.CategoryChecklistItems
            .Where(item => item.CategoryId == categoryId)
            .MaxAsync(item => (int?)item.SortOrder, cancellationToken) ?? 0;
        var entity = new CategoryChecklistItemEntity
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
            DisplayName = name,
            SortOrder = maxSortOrder + 1,
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.CategoryChecklistItems.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<CategoryChecklistItemInfo> RenameAsync(
        Guid id,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        var name = ValidateName(displayName);
        await EnsureUniqueNameAsync(entity.CategoryId, name, id, cancellationToken);

        entity.DisplayName = name;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<CategoryChecklistItemInfo> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        entity.IsActive = isActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        _dbContext.CategoryChecklistItems.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose() => _dbContext.Dispose();

    private static string ValidateName(string displayName)
    {
        var text = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Nama item wajib tidak boleh kosong.", nameof(displayName));
        }

        if (text.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Nama item wajib maksimal {MaximumNameLength} karakter.",
                nameof(displayName));
        }

        if (text.Any(char.IsControl))
        {
            throw new ArgumentException("Nama item wajib mengandung karakter tidak valid.", nameof(displayName));
        }

        return text;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.DocumentCategories.AnyAsync(
                category => category.Id == categoryId,
                cancellationToken))
        {
            throw new InvalidOperationException("Kategori tidak ditemukan.");
        }
    }

    private async Task EnsureUniqueNameAsync(
        Guid categoryId,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var normalized = name.ToLowerInvariant();
        var duplicate = await _dbContext.CategoryChecklistItems.AnyAsync(
            item => item.CategoryId == categoryId
                && item.DisplayName.ToLower() == normalized
                && (!excludedId.HasValue || item.Id != excludedId.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Item wajib '{name}' sudah ada di kategori ini.");
        }
    }

    private async Task<CategoryChecklistItemEntity> FindEntityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.CategoryChecklistItems
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity ?? throw new InvalidOperationException("Item wajib tidak ditemukan.");
    }

    private static CategoryChecklistItemInfo Map(CategoryChecklistItemEntity entity) =>
        new(entity.Id, entity.CategoryId, entity.DisplayName, entity.SortOrder, entity.IsActive);
}
