using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
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
    private readonly ICollectionView _documentView;
    private readonly ObservableCollection<ProjectCategoryRow> _categories = new();
    private readonly ObservableCollection<ProjectDocumentRow> _documents = new();
    private readonly ObservableCollection<ProjectFieldInfo> _fields = new();
    private Guid _projectId;
    private ProjectInfo? _project;
    private ProjectChecklistSummary? _checklist;
    private bool _isLoading;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;
    private int _progressPercent;
    private string _progressText = string.Empty;
    private string _documentFilter = "Semua";
    private int _completedRequiredCount;

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
        BackCommand = new RelayCommand(Back);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanOperate);
        UploadFileCommand = new AsyncRelayCommand<ProjectCategoryRow>(UploadAsync, _ => CanOperate());
        OpenDocumentCommand = new AsyncRelayCommand<ProjectDocumentRow>(OpenDocumentAsync, _ => CanOperate());
        RevealDocumentCommand = new AsyncRelayCommand<ProjectDocumentRow>(RevealDocumentAsync, _ => CanOperate());
        OpenCategoryFolderCommand = new AsyncRelayCommand<ProjectCategoryRow>(OpenCategoryFolderAsync, _ => CanOperate());
        OpenProjectFolderCommand = new AsyncRelayCommand(OpenProjectFolderAsync, CanOperate);
        EditDocumentCommand = new RelayCommand<ProjectDocumentRow>(
            EditDocument,
            row => CanOperate() && row?.CanEditDocument == true);
        MarkCompleteCommand = new AsyncRelayCommand<ProjectDocumentRow>(
            MarkCompleteAsync,
            row => CanOperate() && row?.CanMarkComplete == true);
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
            var result = await _projectStore.RefreshFilesystemAsync(_projectId, CreateProgress());
            ApplyDetail(result.Detail);
            SetStatus(result.Message, true);
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

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ProgressPercent = 0;
        ProgressText = string.Empty;
        OnPropertyChanged(nameof(HasProgress));
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, false);
        }
        finally
        {
            ProgressPercent = 100;
            IsBusy = false;
            OnPropertyChanged(nameof(HasProgress));
        }
    }

    private IProgress<ProjectCreationProgressInfo> CreateProgress() =>
        new Progress<ProjectCreationProgressInfo>(report =>
        {
            ProgressPercent = report.Percent;
            ProgressText = $"{report.Stage}: {report.Message}";
            OnPropertyChanged(nameof(HasProgress));
        });

    private void ApplyDetail(ProjectDetailInfo detail)
    {
        Project = detail.Project;
        Checklist = detail.Checklist;
        _categories.Clear();
        foreach (var category in detail.Categories)
        {
            _categories.Add(new ProjectCategoryRow(category));
        }

        _documents.Clear();
        foreach (var document in detail.Documents)
        {
            _documents.Add(new ProjectDocumentRow(document));
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
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
