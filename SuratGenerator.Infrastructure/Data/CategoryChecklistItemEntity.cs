namespace SuratGenerator.Infrastructure.Data;

public sealed class CategoryChecklistItemEntity
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
