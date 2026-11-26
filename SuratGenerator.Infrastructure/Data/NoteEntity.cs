namespace SuratGenerator.Infrastructure.Data;

public sealed class NoteEntity
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string NormalizedTitle { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
