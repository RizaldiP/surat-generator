namespace SuratGenerator.Infrastructure.Data;

public sealed class PlaceholderMappingEntity
{
    public Guid Id { get; set; }

    public string PlaceholderKey { get; set; } = string.Empty;

    public string ProfileFieldKey { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; }
}
