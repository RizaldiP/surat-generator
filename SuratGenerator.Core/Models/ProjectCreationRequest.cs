namespace SuratGenerator.Core.Models;

public sealed record ProjectCreationRequest(
    string ShipName,
    string JobName,
    IReadOnlyList<ProjectFieldRequest> Fields);
