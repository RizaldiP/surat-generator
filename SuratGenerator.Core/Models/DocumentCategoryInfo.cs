namespace SuratGenerator.Core.Models;

public sealed record DocumentCategoryInfo(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsActive,
    string Version,
    DateTimeOffset UpdatedAt);
