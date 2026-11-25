namespace SuratGenerator.Core.Models;

public sealed record DocumentFieldEditSaveRequest(
    Guid ProjectDocumentId,
    Guid ProjectId,
    string OutputPath,
    IReadOnlyList<DocumentFieldValueSaveInfo> Values);
