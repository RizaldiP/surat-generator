namespace SuratGenerator.Infrastructure.Data;

public sealed class SchemaVersionEntity
{
    public string Version { get; set; } = string.Empty;

    public DateTimeOffset AppliedAt { get; set; }
}
