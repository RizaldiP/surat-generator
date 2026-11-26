using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class NoteItemStore : INoteItemStore, IDisposable
{
    private const int MaximumNameLength = 100;
    private const int MaximumDescriptionLength = 500;

    private readonly SuratGeneratorDbContext _dbContext;

    public NoteItemStore()
        : this(DatabaseOptionsFactory.CreateProduction())
    {
    }

    public NoteItemStore(IAppLogger? logger)
        : this()
    {
        _ = logger ?? NullAppLogger.Instance;
    }

    public NoteItemStore(DbContextOptions<SuratGeneratorDbContext> options)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<IReadOnlyList<NoteItemInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.NoteItems
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<NoteItemInfo>> GetByNoteAsync(
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.NoteItems
            .AsNoTracking()
            .Where(item => item.NoteId == noteId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task<NoteItemInfo> AddAsync(
        Guid noteId,
        string displayName,
        string description,
        CancellationToken cancellationToken = default)
    {
        var name = ValidateName(displayName);
        var text = ValidateDescription(description);
        await EnsureNoteExistsAsync(noteId, cancellationToken);
        await EnsureUniqueNameAsync(noteId, name, null, cancellationToken);

        var maxSortOrder = await _dbContext.NoteItems
            .Where(item => item.NoteId == noteId)
            .MaxAsync(item => (int?)item.SortOrder, cancellationToken) ?? 0;
        var entity = new NoteItemEntity
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            DisplayName = name,
            Description = text,
            SortOrder = maxSortOrder + 1,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.NoteItems.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<NoteItemInfo> UpdateAsync(
        Guid id,
        string displayName,
        string description,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        var name = ValidateName(displayName);
        var text = ValidateDescription(description);
        await EnsureUniqueNameAsync(entity.NoteId, name, id, cancellationToken);

        entity.DisplayName = name;
        entity.Description = text;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task ReorderAsync(
        Guid noteId,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken = default)
    {
        var ids = orderedIds.Distinct().ToArray();
        if (ids.Length != orderedIds.Count)
        {
            throw new ArgumentException("Urutan syarat mengandung ID duplikat.", nameof(orderedIds));
        }

        var entities = await _dbContext.NoteItems
            .Where(item => item.NoteId == noteId)
            .ToListAsync(cancellationToken);
        if (entities.Count != ids.Length || entities.Any(entity => !ids.Contains(entity.Id)))
        {
            throw new ArgumentException("Urutan syarat harus memuat seluruh syarat catatan ini.", nameof(orderedIds));
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
        var entity = await FindEntityAsync(id, cancellationToken);
        _dbContext.NoteItems.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose() => _dbContext.Dispose();

    private static string ValidateName(string displayName)
    {
        var text = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Nama syarat tidak boleh kosong.", nameof(displayName));
        }

        if (text.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Nama syarat maksimal {MaximumNameLength} karakter.",
                nameof(displayName));
        }

        if (text.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
        {
            throw new ArgumentException("Nama syarat mengandung karakter tidak valid.", nameof(displayName));
        }

        return text;
    }

    private static string ValidateDescription(string description)
    {
        var text = (description ?? string.Empty).Trim();
        if (text.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"Keterangan maksimal {MaximumDescriptionLength} karakter.",
                nameof(description));
        }

        if (text.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
        {
            throw new ArgumentException("Keterangan mengandung karakter tidak valid.", nameof(description));
        }

        return text;
    }

    private async Task EnsureNoteExistsAsync(Guid noteId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Notes.AnyAsync(note => note.Id == noteId, cancellationToken))
        {
            throw new InvalidOperationException("Catatan tidak ditemukan.");
        }
    }

    private async Task EnsureUniqueNameAsync(
        Guid noteId,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var normalized = name.ToUpperInvariant();
        var duplicate = await _dbContext.NoteItems.AnyAsync(
            item => item.NoteId == noteId
                && item.DisplayName.ToUpper() == normalized
                && (!excludedId.HasValue || item.Id != excludedId.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"Syarat '{name}' sudah ada di catatan ini.");
        }
    }

    private async Task<NoteItemEntity> FindEntityAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.NoteItems
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity ?? throw new InvalidOperationException("Syarat catatan tidak ditemukan.");
    }

    private static NoteItemInfo Map(NoteItemEntity entity) =>
        new(
            entity.Id,
            entity.NoteId,
            entity.DisplayName,
            entity.Description,
            entity.SortOrder,
            entity.UpdatedAt);
}
