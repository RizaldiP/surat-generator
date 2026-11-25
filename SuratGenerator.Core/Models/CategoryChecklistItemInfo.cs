namespace SuratGenerator.Core.Models;

public sealed record CategoryChecklistItemInfo(
    Guid Id,
    Guid CategoryId,
    string DisplayName,
    int SortOrder,
    bool IsActive);
