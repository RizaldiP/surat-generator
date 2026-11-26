namespace SuratGenerator.Infrastructure.Data;

public sealed class NoteItemEntity
{
    public Guid Id { get; set; }

    public Guid NoteId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
