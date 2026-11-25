namespace SuratGenerator.Core.Models;

public sealed record ProjectUploadResult(
    IReadOnlyList<ProjectDocumentInfo> Documents,
    IReadOnlyList<string> SkippedFiles,
    string Message);
