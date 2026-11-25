namespace SuratGenerator.Core.Models;

public sealed record TemplateFolderScanSummary(
    int Imported,
    int Updated,
    int Unchanged,
    int Missing,
    int Errors,
    IReadOnlyList<string> Messages)
{
    public bool HasIssues => Errors > 0;

    public string SummaryText =>
        $"Baru {Imported}, diperbarui {Updated}, tidak berubah {Unchanged}, hilang {Missing}, gagal {Errors}.";
}
