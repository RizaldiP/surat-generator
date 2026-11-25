namespace SuratGenerator.Core.Models;

public sealed record BackupRecordInfo(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Location,
    string Kind,
    string Status,
    bool IsVerified,
    int ItemCount,
    long SizeBytes,
    string? Message);
