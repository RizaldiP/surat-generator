namespace SuratGenerator.Core.Models;

public sealed record ProjectCategoryInfo(
    Guid Id,
    Guid ProjectId,
    Guid? SourceCategoryId,
    string Name,
    int SortOrder,
    string FolderPath);
