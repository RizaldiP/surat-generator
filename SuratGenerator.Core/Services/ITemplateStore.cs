using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public interface ITemplateStore
{
    Task<TemplateInfo> SaveAsync(
        string sourceFilePath,
        IReadOnlyList<TemplateField> fields,
        CancellationToken cancellationToken = default);

    Task<TemplateInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TemplateInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TemplateInfo> ReplaceFileAsync(
        Guid id,
        string sourceFilePath,
        IReadOnlyList<TemplateField> fields,
        string sourceFileName,
        CancellationToken cancellationToken = default);

    Task<TemplateInfo?> GetBySourcePathAsync(string sourceFilePath, CancellationToken cancellationToken = default);

    Task<TemplateInfo> SetSourceAsync(
        Guid id,
        string sourceFilePath,
        string checksum,
        DateTimeOffset modifiedAt,
        CancellationToken cancellationToken = default);

    Task UpdateMetadataAsync(
        Guid id,
        string name,
        string category,
        string version,
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
