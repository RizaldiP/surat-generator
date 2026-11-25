namespace SuratGenerator.Core.Models;

public sealed record DocumentFieldEditInfo(
    Guid ProjectDocumentId,
    Guid ProjectId,
    string DisplayName,
    string BaseTemplatePath,
    string OutputPath,
    IReadOnlyList<DocumentFieldValueInfo> Values,
    DateTimeOffset? LastEditedAt);
