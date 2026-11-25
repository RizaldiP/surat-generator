namespace SuratGenerator.Core.Models;

public sealed record PlaceholderMappingInfo(
    Guid Id,
    string PlaceholderKey,
    string ProfileFieldKey,
    bool IsActive,
    DateTimeOffset UpdatedAt);
