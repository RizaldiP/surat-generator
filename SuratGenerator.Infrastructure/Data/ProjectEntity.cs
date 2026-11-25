namespace SuratGenerator.Infrastructure.Data;

public sealed class ProjectEntity
{
    public Guid Id { get; set; }

    public string ShipName { get; set; } = string.Empty;

    public string JobName { get; set; } = string.Empty;

    public string RootPath { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string StructureVersion { get; set; } = string.Empty;

    public bool IsMissing { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
