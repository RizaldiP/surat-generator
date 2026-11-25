namespace SuratGenerator.Infrastructure.Data;

public sealed class DocumentCategoryEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public string Version { get; set; } = "1.0";

    public DateTimeOffset UpdatedAt { get; set; }
}
