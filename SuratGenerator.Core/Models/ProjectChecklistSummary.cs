namespace SuratGenerator.Core.Models;

public sealed record ProjectChecklistSummary(
    int RequiredCount,
    int NotAvailableCount,
    int IncompleteCount,
    int InProgressCount,
    int CompletedCount,
    int CompletedRequiredCount,
    int ProgressPercent);
