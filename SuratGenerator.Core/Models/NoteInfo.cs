namespace SuratGenerator.Core.Models;

public sealed record NoteInfo(
    Guid Id,
    string Title,
    int SortOrder,
    DateTimeOffset UpdatedAt);
