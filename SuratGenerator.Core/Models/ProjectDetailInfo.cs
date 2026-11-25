namespace SuratGenerator.Core.Models;

public sealed record ProjectDetailInfo(
    ProjectInfo Project,
    IReadOnlyList<ProjectCategoryInfo> Categories,
    IReadOnlyList<ProjectDocumentInfo> Documents,
    IReadOnlyList<ProjectFieldInfo> Fields,
    ProjectChecklistSummary Checklist,
    IReadOnlyList<string>? Warnings = null);
