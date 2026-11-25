namespace SuratGenerator.Core.Models;

public sealed record ProjectRefreshResult(
    ProjectDetailInfo Detail,
    IReadOnlyList<ProjectRefreshChange> Changes,
    string Message);
