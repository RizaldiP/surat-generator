namespace SuratGenerator.Core.Models;

public sealed record MasterStructureSnapshot(
    string Version,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DocumentCategoryInfo> Categories);
