using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class DraftStore : IDraftStore, IDisposable
{
    private readonly SuratGeneratorDbContext _dbContext;

    public DraftStore()
        : this(DatabaseOptionsFactory.CreateProduction())
    {
    }

    public DraftStore(DbContextOptions<SuratGeneratorDbContext> options)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<DraftInfo> SaveAsync(
        DraftInfo draft,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = await _dbContext.Drafts.FindAsync([draft.Id], cancellationToken);

        if (entity is null)
        {
            entity = new DraftEntity
            {
                Id = draft.Id,
                CreatedAt = now
            };
            _dbContext.Drafts.Add(entity);
        }

        entity.TemplateId = draft.TemplateId;
        entity.TemplateName = draft.TemplateName;
        entity.Name = draft.Name;
        entity.ValuesJson = JsonSerializer.Serialize(draft.Values);
        entity.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return draft with { CreatedAt = entity.CreatedAt, UpdatedAt = now };
    }

    public async Task<DraftInfo?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Drafts
            .AsNoTracking()
            .FirstOrDefaultAsync(draft => draft.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<DraftInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Drafts
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(draft => draft.UpdatedAt)
            .Select(Map)
            .ToArray();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static DraftInfo Map(DraftEntity entity)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ValuesJson)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new DraftInfo(
            entity.Id,
            entity.TemplateId,
            entity.TemplateName,
            entity.Name,
            values,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
