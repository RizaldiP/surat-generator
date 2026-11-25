namespace SuratGenerator.Core.Models;

public sealed record DocumentFieldEditField(
    string Key,
    string CanonicalKey,
    string Label,
    string Value,
    string? ProfileFieldKey,
    bool IsOverride);
