using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class NoteStore : INoteStore, IDisposable
{
    private const int MaximumTitleLength = 80;

    private readonly SuratGeneratorDbContext _dbContext;

    public NoteStore()
        : this(DatabaseOptionsFactory.CreateProduction())
    {
    }

    public NoteStore(IAppLogger? logger)
        : this()
    {
        _ = logger ?? NullAppLogger.Instance;
    }

    public NoteStore(DbContextOptions<SuratGeneratorDbContext> options)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<IReadOnlyList<NoteInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Notes
            .AsNoTracking()
            .OrderBy(note => note.SortOrder)
            .ThenBy(note => note.Title)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task<NoteInfo> AddAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        var text = ValidateTitle(title);
        var normalized = NormalizeTitle(text);
        await EnsureUniqueTitleAsync(text, normalized, null, cancellationToken);

        var maxSortOrder = await _dbContext.Notes
            .MaxAsync(note => (int?)note.SortOrder, cancellationToken) ?? 0;
        var entity = new NoteEntity
        {
            Id = Guid.NewGuid(),
            Title = text,
            NormalizedTitle = normalized,
            SortOrder = maxSortOrder + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Notes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<NoteInfo> RenameAsync(
        Guid id,
        string title,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        var text = ValidateTitle(title);
        var normalized = NormalizeTitle(text);
        await EnsureUniqueTitleAsync(text, normalized, id, cancellationToken);

        entity.Title = text;
        entity.NormalizedTitle = normalized;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task ReorderAsync(
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default)
    {
        var ids = orderedIds.Distinct().ToArray();
        if (ids.Length != orderedIds.Count)
        {
            throw new ArgumentException("Urutan catatan mengandung ID duplikat.", nameof(orderedIds));
        }

        var entities = await _dbContext.Notes.ToListAsync(cancellationToken);
        if (entities.Count != ids.Length || entities.Any(entity => !ids.Contains(entity.Id)))
        {
            throw new ArgumentException("Urutan catatan harus memuat seluruh catatan.", nameof(orderedIds));
        }

        var byId = entities.ToDictionary(entity => entity.Id);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < orderedIds.Count; index++)
        {
            var entity = byId[orderedIds[index]];
            entity.SortOrder = index + 1;
            entity.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await FindEntityAsync(id, cancellationToken);
            var items = await _dbContext.NoteItems
                .Where(item => item.NoteId == id)
                .ToListAsync(cancellationToken);
            _dbContext.NoteItems.RemoveRange(items);
            _dbContext.Notes.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var remaining = await _dbContext.Notes
                .OrderBy(note => note.SortOrder)
                .ThenBy(note => note.Title)
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

    public void Dispose() => _dbContext.Dispose();

    private static string ValidateTitle(string title)
    {
        var text = (title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Judul catatan tidak boleh kosong.", nameof(title));
        }

        if (text.Length > MaximumTitleLength)
        {
            throw new ArgumentException(
                $"Judul catatan maksimal {MaximumTitleLength} karakter.",
                nameof(title));
        }

        if (text.Any(char.IsControl))
        {
            throw new ArgumentException("Judul catatan mengandung karakter tidak valid.", nameof(title));
        }

        return text;
    }

    private static string NormalizeTitle(string title) => title.Trim().ToUpperInvariant();

    private async Task EnsureUniqueTitleAsync(
        string title,
        string normalizedTitle,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var duplicate = await _dbContext.Notes.AnyAsync(
            note => note.NormalizedTitle == normalizedTitle
                && (!excludedId.HasValue || note.Id != excludedId.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Catatan '{title}' sudah ada.");
        }
    }

    private async Task<NoteEntity> FindEntityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Notes
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken);
        return entity ?? throw new InvalidOperationException("Catatan tidak ditemukan.");
    }

    private static NoteInfo Map(NoteEntity entity) =>
        new(entity.Id, entity.Title, entity.SortOrder, entity.UpdatedAt);
}
