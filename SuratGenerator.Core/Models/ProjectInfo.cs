namespace SuratGenerator.Core.Models;

public sealed record ProjectInfo(
    Guid Id,
    string ShipName,
    string JobName,
    string RootPath,
    string Status,
    string StructureVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
