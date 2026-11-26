namespace SuratGenerator.Core.Models;

public sealed record NoteItemInfo(
    Guid Id,
    Guid NoteId,
    string DisplayName,
    string Description,
    int SortOrder,
    DateTimeOffset UpdatedAt);
