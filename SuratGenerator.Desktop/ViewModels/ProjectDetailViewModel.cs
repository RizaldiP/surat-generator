using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Messages;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class ProjectDetailViewModel : ObservableObject
{
    private readonly IProjectStore _projectStore;
    private readonly INavigationService _navigation;
    private readonly IFilePickerService _filePicker;
    private readonly IProjectDialogService _dialogs;
    private readonly IExplorerService _explorer;
    private readonly IConfirmationService _confirmations;
    private readonly IDocumentFieldValueStore _fieldValues;
    private static readonly TimeSpan ProgressCompletionHold = TimeSpan.FromMilliseconds(180);
    private readonly ICollectionView _documentView;
    private readonly ObservableCollection<ProjectCategoryRow> _categories = new();
    private readonly ObservableCollection<ProjectDocumentRow> _documents = new();
    private readonly ObservableCollection<ProjectFieldInfo> _fields = new();
    private Guid _projectId;
    private ProjectInfo? _project;
    private ProjectChecklistSummary? _checklist;
    private bool _isLoading;
    private bool _isBusy;
    private int _progressGeneration;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;
    private int _progressPercent;
    private string _progressText = string.Empty;
    private string _documentFilter = "Semua";
    private int _completedRequiredCount;
    private bool _isFolderMissing;
    private IReadOnlyList<string> _warnings = Array.Empty<string>();

    public ProjectDetailViewModel(
        IProjectStore projectStore,
        INavigationService navigation,
        IFilePickerService filePicker,
        IProjectDialogService dialogs,
        IExplorerService explorer,
        IConfirmationService confirmations,
        IDocumentFieldValueStore fieldValues)
    {
        _projectStore = projectStore;
        _navigation = navigation;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _explorer = explorer;
        _confirmations = confirmations;
        _fieldValues = fieldValues;
        Categories = new ReadOnlyObservableCollection<ProjectCategoryRow>(_categories);
        Documents = new ReadOnlyObservableCollection<ProjectDocumentRow>(_documents);
        Fields = new ReadOnlyObservableCollection<ProjectFieldInfo>(_fields);
        _documentView = CollectionViewSource.GetDefaultView(_documents);
        _documentView.Filter = FilterDocument;
        _documentView.GroupDescriptions.Add(new PropertyGroupDescription("CategoryName"));
        _documentView.SortDescriptions.Add(new SortDescription("CategoryName", ListSortDirection.Ascending));
        _documentView.SortDescriptions.Add(new SortDescription("DisplayName", ListSortDirection.Ascending));
        BackCommand = new RelayCommand(Back);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanOperate);
        UploadFileCommand = new AsyncRelayCommand<ProjectCategoryRow>(UploadAsync, _ => CanOperate() && !IsFolderMissing);
        OpenDocumentCommand = new AsyncRelayCommand<ProjectDocumentRow>(OpenDocumentAsync, _ => CanOperate() && !IsFolderMissing);
        RevealDocumentCommand = new AsyncRelayCommand<ProjectDocumentRow>(RevealDocumentAsync, _ => CanOperate() && !IsFolderMissing);
        OpenCategoryFolderCommand = new AsyncRelayCommand<ProjectCategoryRow>(OpenCategoryFolderAsync, _ => CanOperate() && !IsFolderMissing);
        OpenProjectFolderCommand = new AsyncRelayCommand(OpenProjectFolderAsync, () => CanOperate() && !IsFolderMissing);
        EditDocumentCommand = new RelayCommand<ProjectDocumentRow>(
            EditDocument,
            row => CanOperate() && !IsFolderMissing && row?.CanEditDocument == true);
        MarkCompleteCommand = new AsyncRelayCommand<ProjectDocumentRow>(
            MarkCompleteAsync,
            row => CanOperate() && !IsFolderMissing && row?.CanMarkComplete == true);
        DeleteDocumentCommand = new AsyncRelayCommand<ProjectDocumentRow>(
            DeleteDocumentAsync,
            row => CanOperate() && row is not null);
        DeleteFromListCommand = new AsyncRelayCommand(DeleteFromListAsync, () => CanOperate() && IsFolderMissing);

        WeakReferenceMessenger.Default.Register<ProjectsChangedMessage>(
            this,
            (_, _) => _ = ReloadFromWatcherAsync());
    }

    public ReadOnlyObservableCollection<ProjectCategoryRow> Categories { get; }

    public ReadOnlyObservableCollection<ProjectDocumentRow> Documents { get; }

    public ICollectionView DocumentsView => _documentView;

    public ReadOnlyObservableCollection<ProjectFieldInfo> Fields { get; }

    public IReadOnlyList<string> DocumentFilters { get; } = new[] { "Semua", "Wajib", "Belum Selesai", "Selesai" };

    public string DocumentFilter
    {
        get => _documentFilter;
        set
        {
            if (SetProperty(ref _documentFilter, value))
            {
                _documentView.Refresh();
            }
        }
    }

    public IRelayCommand BackCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand<ProjectCategoryRow> UploadFileCommand { get; }

    public IAsyncRelayCommand<ProjectDocumentRow> OpenDocumentCommand { get; }

    public IAsyncRelayCommand<ProjectDocumentRow> RevealDocumentCommand { get; }

    public IAsyncRelayCommand<ProjectCategoryRow> OpenCategoryFolderCommand { get; }

    public IAsyncRelayCommand OpenProjectFolderCommand { get; }

    public IRelayCommand<ProjectDocumentRow> EditDocumentCommand { get; }

    public IAsyncRelayCommand<ProjectDocumentRow> MarkCompleteCommand { get; }

    public IAsyncRelayCommand<ProjectDocumentRow> DeleteDocumentCommand { get; }

    public IAsyncRelayCommand DeleteFromListCommand { get; }

    public ProjectInfo? Project
    {
        get => _project;
        private set
        {
            if (SetProperty(ref _project, value))
            {
                OnPropertyChanged(nameof(HasProject));
                NotifyCommands();
            }
        }
    }

    public ProjectChecklistSummary? Checklist
    {
        get => _checklist;
        private set
        {
            if (SetProperty(ref _checklist, value))
            {
                OnPropertyChanged(nameof(ChecklistText));
                OnPropertyChanged(nameof(HasChecklist));
            }
        }
    }

    public bool HasCategories => _categories.Count > 0;

    public bool HasDocuments => _documents.Count > 0;

    public bool HasFields => _fields.Count > 0;

    public bool HasProject => Project is not null;

    public bool HasChecklist => Checklist is not null;

    public bool IsFolderMissing
    {
        get => _isFolderMissing;
        private set
        {
            if (SetProperty(ref _isFolderMissing, value))
            {
                NotifyCommands();
            }
        }
    }

    public IReadOnlyList<string> Warnings
    {
        get => _warnings;
        private set
        {
            if (SetProperty(ref _warnings, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(WarningText));
            }
        }
    }

    public bool HasWarnings => Warnings.Count > 0;

    public string WarningText => string.Join("\n", Warnings);

    public int CompletedRequiredCount
    {
        get => _completedRequiredCount;
        private set => SetProperty(ref _completedRequiredCount, value);
    }

    public string ChecklistText => Checklist is null
        ? string.Empty
        : $"Wajib {Checklist.RequiredCount} • Belum tersedia {Checklist.NotAvailableCount} • Belum lengkap {Checklist.IncompleteCount} • Dalam proses {Checklist.InProgressCount} • Selesai {Checklist.CompletedCount}";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyCommands();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyCommands();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsStatusSuccess
    {
        get => _isStatusSuccess;
        private set => SetProperty(ref _isStatusSuccess, value);
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public bool HasProgress => IsBusy && ProgressPercent > 0;

    public async Task InitializeAsync(Guid projectId)
    {
        _projectId = projectId;
        IsLoading = true;
        try
        {
            var detail = await _projectStore.GetDetailAsync(projectId);
            if (detail is null)
            {
                Project = null;
                SetStatus("Pekerjaan tidak ditemukan.", false);
                return;
            }

            ApplyDetail(detail);
            SetStatus($"Snapshot {detail.Project.StructureVersion} dimuat.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memuat detail pekerjaan: {exception.Message}", false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanOperate() => !IsBusy && !IsLoading && Project is not null;

    private async Task RefreshAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _projectStore.RefreshFilesystemAsync(
                _projectId,
                CreateProgress(),
                pullMissingMasters: true);
            ApplyDetail(result.Detail);
            SetStatus(result.Message, true);
        });
    }

    private async Task ReloadFromWatcherAsync()
    {
        if (_projectId == Guid.Empty || IsBusy || IsLoading)
        {
            return;
        }

        try
        {
            var detail = await _projectStore.GetDetailAsync(_projectId);
            if (detail is null)
            {
                Project = null;
                SetStatus("Pekerjaan tidak ditemukan.", false);
                return;
            }

            ApplyDetail(detail);
            SetStatus(
                detail.Project.IsMissing
                    ? "Folder pekerjaan tidak ditemukan; pekerjaan disembunyikan dari daftar."
                    : $"Snapshot {detail.Project.StructureVersion} dimuat ulang.",
                !detail.Project.IsMissing);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memuat ulang detail pekerjaan: {exception.Message}", false);
        }
    }

    private async Task DeleteFromListAsync()
    {
        var project = Project;
        if (project is null)
        {
            return;
        }

        if (!_confirmations.ConfirmDeleteProject(project.ShipName, project.JobName))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var deleted = await _projectStore.DeleteAsync(_projectId);
            if (!deleted)
            {
                SetStatus("Pekerjaan sudah tidak ada.", false);
                return;
            }

            Back();
        });
    }

    private async Task UploadAsync(ProjectCategoryRow? category)
    {
        if (category is null)
        {
            return;
        }

        var files = _filePicker.PickProjectFiles();
        if (files.Count == 0)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _projectStore.UploadDocumentsAsync(
                _projectId,
                category.Id,
                files,
                (collision, _) => Task.FromResult(_dialogs.ShowFileCollision(collision)),
                CreateProgress());
            var detail = await _projectStore.GetDetailAsync(_projectId);
            if (detail is not null)
            {
                ApplyDetail(detail);
            }

            SetStatus(result.Message, true);
        });
    }

    private async Task OpenDocumentAsync(ProjectDocumentRow? row)
    {
        if (row is null || !row.HasFile)
        {
            SetStatus("Dokumen ini belum memiliki file.", false);
            return;
        }

        await RunAsync(async () =>
        {
            var path = await _projectStore.ResolveDocumentPathAsync(row.Id);
            _explorer.OpenFile(path);
        });
    }

    private async Task RevealDocumentAsync(ProjectDocumentRow? row)
    {
        if (row is null || !row.HasFile)
        {
            SetStatus("Dokumen ini belum memiliki file.", false);
            return;
        }

        await RunAsync(async () =>
        {
            var path = await _projectStore.ResolveDocumentPathAsync(row.Id);
            _explorer.RevealFile(path);
        });
    }

    private async Task OpenCategoryFolderAsync(ProjectCategoryRow? category)
    {
        if (category is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var path = await _projectStore.ResolveCategoryPathAsync(category.Id);
            _explorer.OpenFolder(path);
        });
    }

    private async Task OpenProjectFolderAsync()
    {
        await RunAsync(async () =>
        {
            var path = await _projectStore.ResolveProjectPathAsync(_projectId);
            _explorer.OpenFolder(path);
        });
    }

    private void EditDocument(ProjectDocumentRow? row)
    {
        if (row is null || !row.CanEditDocument)
        {
            SetStatus("Hanya file Word .docx yang dapat diisi melalui editor.", false);
            return;
        }

        _navigation.NavigateTo<Views.Pages.ProjectDocumentEditorPage>(row.Id);
    }

    private bool FilterDocument(object item)
    {
        if (item is not ProjectDocumentRow row)
        {
            return false;
        }

        return DocumentFilter switch
        {
            "Wajib" => row.IsRequired,
            "Belum Selesai" => row.IsRequired
                && !string.Equals(row.Status, DocumentStatuses.Completed, StringComparison.Ordinal),
            "Selesai" => string.Equals(row.Status, DocumentStatuses.Completed, StringComparison.Ordinal),
            _ => true
        };
    }

    private async Task MarkCompleteAsync(ProjectDocumentRow? row)
    {
        if (row is null || !row.CanMarkComplete)
        {
            return;
        }

        if (!_confirmations.ConfirmMarkComplete(row.DisplayName))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (row.IsWordDocument)
            {
                var unfilled = await _fieldValues.GetUnfilledRequiredPlaceholdersAsync(row.Id);
                if (unfilled.Count > 0)
                {
                    SetStatus(
                        $"Dokumen '{row.DisplayName}' belum bisa Selesai. Place holder kosong: {string.Join(", ", unfilled)}",
                        false);
                    return;
                }
            }

            var updated = await _projectStore.SetDocumentStatusAsync(row.Id, DocumentStatuses.Completed);
            var detail = await _projectStore.GetDetailAsync(_projectId);
            if (detail is not null)
            {
                ApplyDetail(detail);
            }

            SetStatus($"'{updated.DisplayName}' ditandai Selesai.", true);
        });
    }

    private async Task DeleteDocumentAsync(ProjectDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_confirmations.ConfirmDeleteDocument(row.DisplayName, row.HasFile))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var deleted = await _projectStore.DeleteDocumentAsync(row.Id);
            if (!deleted)
            {
                SetStatus("Dokumen sudah tidak ada di pekerjaan ini.", false);
                return;
            }

            var detail = await _projectStore.GetDetailAsync(_projectId);
            if (detail is not null)
            {
                ApplyDetail(detail);
            }

            SetStatus($"'{row.DisplayName}' dihapus dari pekerjaan.", true);
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        var generation = ++_progressGeneration;
        var succeeded = false;
        IsBusy = true;
        ProgressPercent = 0;
        ProgressText = string.Empty;
        OnPropertyChanged(nameof(HasProgress));
        try
        {
            await action();
            succeeded = true;
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, false);
        }
        finally
        {
            if (generation == _progressGeneration)
            {
                if (succeeded)
                {
                    ProgressPercent = 100;
                    ProgressText = "Selesai";
                    OnPropertyChanged(nameof(HasProgress));
                    await Task.Delay(ProgressCompletionHold);
                }

                _progressGeneration++;
                IsBusy = false;
                ProgressPercent = 0;
                ProgressText = string.Empty;
                OnPropertyChanged(nameof(HasProgress));
            }
        }
    }

    private IProgress<ProjectCreationProgressInfo> CreateProgress()
    {
        var generation = _progressGeneration;
        return new Progress<ProjectCreationProgressInfo>(report =>
        {
            if (generation != _progressGeneration || !IsBusy)
            {
                return;
            }

            ProgressPercent = report.Percent;
            ProgressText = $"{report.Stage}: {report.Message}";
            OnPropertyChanged(nameof(HasProgress));
        });
    }

    private void ApplyDetail(ProjectDetailInfo detail)
    {
        Project = detail.Project;
        Checklist = detail.Checklist;
        IsFolderMissing = detail.Project.IsMissing;
        Warnings = detail.Warnings ?? Array.Empty<string>();
        _categories.Clear();
        foreach (var category in detail.Categories)
        {
            _categories.Add(new ProjectCategoryRow(category));
        }

        var categoryNames = detail.Categories.ToDictionary(category => category.Id, category => category.Name);
        _documents.Clear();
        foreach (var document in detail.Documents)
        {
            var categoryName = categoryNames.TryGetValue(document.ProjectCategoryId, out var name) ? name : "-";
            _documents.Add(new ProjectDocumentRow(document, categoryName));
        }

        _fields.Clear();
        foreach (var field in detail.Fields)
        {
            _fields.Add(field);
        }

        CompletedRequiredCount = _documents.Count(document =>
            document.IsRequired
            && string.Equals(document.Status, DocumentStatuses.Completed, StringComparison.Ordinal));
        _documentView.Refresh();

        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(HasFields));
    }

    private void Back() => _navigation.NavigateTo<Views.Pages.ProjectListPage>();

    private void NotifyCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        UploadFileCommand.NotifyCanExecuteChanged();
        OpenDocumentCommand.NotifyCanExecuteChanged();
        RevealDocumentCommand.NotifyCanExecuteChanged();
        OpenCategoryFolderCommand.NotifyCanExecuteChanged();
        OpenProjectFolderCommand.NotifyCanExecuteChanged();
        EditDocumentCommand.NotifyCanExecuteChanged();
        MarkCompleteCommand.NotifyCanExecuteChanged();
        DeleteDocumentCommand.NotifyCanExecuteChanged();
        DeleteFromListCommand.NotifyCanExecuteChanged();
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
