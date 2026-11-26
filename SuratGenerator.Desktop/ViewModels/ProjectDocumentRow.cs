using System.ComponentModel;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class ProjectDocumentRow : INotifyPropertyChanged
{
    private readonly string _categoryName;
    private readonly Action<ProjectDocumentRow, bool, string>? _onCompletionChanged;
    private ProjectDocumentInfo _document;

    public ProjectDocumentRow(
        ProjectDocumentInfo document,
        string categoryName = "",
        Action<ProjectDocumentRow, bool, string>? onCompletionChanged = null)
    {
        _document = document;
        _categoryName = categoryName;
        _onCompletionChanged = onCompletionChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProjectDocumentInfo Document => _document;

    public Guid Id => _document.Id;

    public string CategoryName => _categoryName;

    public string DisplayName => _document.DisplayName;

    public string FileType => string.IsNullOrWhiteSpace(_document.FileType) ? "-" : _document.FileType;

    public string Source => _document.Source;

    public string Status => _document.Status;

    public string VersionSnapshot => _document.VersionSnapshot;

    public string FilePath => _document.FilePath;

    public bool HasFile => !string.IsNullOrWhiteSpace(_document.FilePath);

    public bool IsWordDocument => string.Equals(_document.FileType, ".docx", StringComparison.OrdinalIgnoreCase);

    public bool CanEditDocument => HasFile && IsWordDocument;

    public bool IsRequired => _document.IsRequired;

    public bool CanToggleComplete => HasFile
        && !string.Equals(_document.Status, DocumentStatuses.NotAvailable, StringComparison.Ordinal);

    public bool IsCompleted
    {
        get => string.Equals(_document.Status, DocumentStatuses.Completed, StringComparison.Ordinal);
        set
        {
            if (value == IsCompleted || !CanToggleComplete)
            {
                return;
            }

            var previousStatus = _document.Status;
            ApplyStatus(
                value ? DocumentStatuses.Completed : DocumentStatuses.Incomplete,
                previousStatus,
                notifyExternal: true);
        }
    }

    public string SizeText => FormatSize(_document.FileSizeBytes);

    public string ModifiedText => _document.LastModifiedUtc?.ToLocalTime().ToString("dd MMM yyyy HH:mm")
        ?? "-";

    public void SetStatus(string status)
    {
        if (string.Equals(status, _document.Status, StringComparison.Ordinal))
        {
            return;
        }

        ApplyStatus(status, _document.Status, notifyExternal: false);
    }

    private void ApplyStatus(string status, string previousStatus, bool notifyExternal)
    {
        _document = _document with { Status = status };
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(CanToggleComplete));
        if (notifyExternal)
        {
            _onCompletionChanged?.Invoke(this, IsCompleted, previousStatus);
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
