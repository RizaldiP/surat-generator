namespace SuratGenerator.Core.Models;

public sealed record DocumentFieldValueSaveInfo(
    string Key,
    string Value,
    bool IsOverride);
