namespace SuratGenerator.Core.Models;

public sealed record DocumentFieldValueInfo(
    Guid Id,
    Guid ProjectDocumentId,
    string Key,
    string Value,
    bool IsOverride,
    DateTimeOffset UpdatedAt);
