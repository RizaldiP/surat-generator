namespace SuratGenerator.Core.Models;

public sealed record ProjectFieldInfo(
    Guid Id,
    Guid ProjectId,
    string Key,
    string Value,
    string FieldType,
    string Source);
