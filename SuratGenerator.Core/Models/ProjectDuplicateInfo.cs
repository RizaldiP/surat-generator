namespace SuratGenerator.Core.Models;

public sealed record ProjectDuplicateInfo(
    Guid? ProjectId,
    string ShipName,
    string JobName,
    string RootPath);
