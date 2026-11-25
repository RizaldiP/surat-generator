namespace SuratGenerator.Infrastructure.Data;

public sealed class BackupRecordEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Location { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool IsVerified { get; set; }

    public int ItemCount { get; set; }

    public long SizeBytes { get; set; }

    public string? Message { get; set; }
}
