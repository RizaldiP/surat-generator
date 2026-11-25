namespace SuratGenerator.Core.Models;

public sealed record SyncSummaryInfo(
    int Uploaded,
    int Downloaded,
    int Skipped,
    int Conflicts,
    int Errors,
    IReadOnlyList<string> Messages)
{
    public bool HasIssues => Conflicts > 0 || Errors > 0;

    public string SummaryText =>
        $"Upload {Uploaded}, unduh {Downloaded}, dilewati {Skipped}, konflik {Conflicts}, gagal {Errors}.";
}
