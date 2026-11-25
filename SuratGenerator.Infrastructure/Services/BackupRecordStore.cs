using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class BackupRecordStore : IBackupRecordStore, IDisposable
{
    private readonly SuratGeneratorDbContext _dbContext;
    private readonly IAppLogger _logger;

    public BackupRecordStore()
        : this(DatabaseOptionsFactory.CreateProduction(), NullAppLogger.Instance)
    {
    }

    public BackupRecordStore(IAppLogger? logger)
        : this(DatabaseOptionsFactory.CreateProduction(), logger)
    {
    }

    public BackupRecordStore(DbContextOptions<SuratGeneratorDbContext> options, IAppLogger? logger = null)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        _logger = logger ?? NullAppLogger.Instance;
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<BackupRecordInfo> AddAsync(
        BackupRecordInfo record,
        CancellationToken cancellationToken = default)
    {
        var entity = new BackupRecordEntity
        {
            Id = record.Id == Guid.Empty ? Guid.NewGuid() : record.Id,
            CreatedAt = record.CreatedAt == default ? DateTimeOffset.UtcNow : record.CreatedAt,
            Location = record.Location,
            Kind = record.Kind,
            Status = record.Status,
            IsVerified = record.IsVerified,
            ItemCount = record.ItemCount,
            SizeBytes = record.SizeBytes,
            Message = record.Message
        };

        _dbContext.BackupRecords.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.Info($"Catatan backup disimpan: {entity.Location}");
        return Map(entity);
    }

    public async Task<IReadOnlyList<BackupRecordInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.BackupRecords
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(item => item.CreatedAt)
            .Select(Map)
            .ToArray();
    }

    public async Task<BackupRecordInfo?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BackupRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public void Dispose() => _dbContext.Dispose();

    private static BackupRecordInfo Map(BackupRecordEntity entity) => new(
        entity.Id,
        entity.CreatedAt,
        entity.Location,
        entity.Kind,
        entity.Status,
        entity.IsVerified,
        entity.ItemCount,
        entity.SizeBytes,
        entity.Message);
}
