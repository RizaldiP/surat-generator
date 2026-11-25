using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class GeneratedDocumentStore : IGeneratedDocumentStore, IDisposable
{
    private readonly SuratGeneratorDbContext _dbContext;

    public GeneratedDocumentStore()
        : this(DatabaseOptionsFactory.CreateProduction())
    {
    }

    public GeneratedDocumentStore(DbContextOptions<SuratGeneratorDbContext> options)
    {
        _dbContext = new SuratGeneratorDbContext(options);
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task RecordAsync(
        GeneratedDocumentInfo document,
        CancellationToken cancellationToken = default)
    {
        _dbContext.GeneratedDocuments.Add(new GeneratedDocumentEntity
        {
            Id = document.Id,
            TemplateId = document.TemplateId,
            TemplateName = document.TemplateName,
            DocumentName = document.DocumentName,
            Kind = document.Kind,
            FilePath = document.FilePath,
            FileSizeBytes = document.FileSizeBytes,
            GeneratedAt = document.GeneratedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GeneratedDocumentInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.GeneratedDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(entity => entity.GeneratedAt)
            .Select(entity => new GeneratedDocumentInfo(
                entity.Id,
                entity.TemplateId,
                entity.TemplateName,
                entity.DocumentName,
                entity.Kind,
                entity.FilePath,
                entity.FileSizeBytes,
                entity.GeneratedAt))
            .ToArray();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
