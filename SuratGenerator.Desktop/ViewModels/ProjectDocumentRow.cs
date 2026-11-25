using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class ProjectDocumentRow
{
    public ProjectDocumentRow(ProjectDocumentInfo document, string categoryName = "")
    {
        Document = document;
        CategoryName = categoryName;
    }

    public ProjectDocumentInfo Document { get; }

    public Guid Id => Document.Id;

    public string CategoryName { get; }

    public string DisplayName => Document.DisplayName;

    public string FileType => string.IsNullOrWhiteSpace(Document.FileType) ? "-" : Document.FileType;

    public string Source => Document.Source;

    public string Status => Document.Status;

    public string VersionSnapshot => Document.VersionSnapshot;

    public string FilePath => Document.FilePath;

    public bool HasFile => !string.IsNullOrWhiteSpace(Document.FilePath);

    public bool IsWordDocument => string.Equals(Document.FileType, ".docx", StringComparison.OrdinalIgnoreCase);

    public bool CanEditDocument => HasFile && IsWordDocument;

    public bool IsRequired => Document.IsRequired;

    public bool CanMarkComplete => IsRequired
        && HasFile
        && !string.Equals(Status, DocumentStatuses.Completed, StringComparison.Ordinal)
        && !string.Equals(Status, DocumentStatuses.NotAvailable, StringComparison.Ordinal);

    public string SizeText => FormatSize(Document.FileSizeBytes);

    public string ModifiedText => Document.LastModifiedUtc?.ToLocalTime().ToString("dd MMM yyyy HH:mm")
        ?? "-";

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "-";
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.0} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }
}
