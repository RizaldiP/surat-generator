using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class MasterDocumentViewModel : ObservableObject
{
    private const string AllCategoriesText = "Semua";

    private readonly IMasterDocumentStore _documentStore;
    private readonly IDocumentCategoryStore _categoryStore;
    private readonly IWorkspacePathService _pathService;
    private readonly IFilePickerService _filePickerService;
    private readonly IMasterDocumentDialogService _dialogService;
    private readonly IConfirmationService _confirmationService;
    private readonly ObservableCollection<string> _categories = new();
    private readonly ObservableCollection<MasterDocumentRow> _rows = new();
    private IReadOnlyList<DocumentCategoryInfo> _allCategories = Array.Empty<DocumentCategoryInfo>();
    private IReadOnlyList<MasterDocumentInfo> _allDocuments = Array.Empty<MasterDocumentInfo>();
    private bool _isLoading;
    private string _selectedCategory = AllCategoriesText;
    private string _searchText = string.Empty;
    private string _masterRootPath = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;

    public MasterDocumentViewModel(
        IMasterDocumentStore documentStore,
        IDocumentCategoryStore categoryStore,
        IWorkspacePathService pathService,
        IFilePickerService filePickerService,
        IMasterDocumentDialogService dialogService,
        IConfirmationService confirmationService)
    {
        _documentStore = documentStore;
        _categoryStore = categoryStore;
        _pathService = pathService;
        _filePickerService = filePickerService;
        _dialogService = dialogService;
        _confirmationService = confirmationService;

        Categories = new ReadOnlyObservableCollection<string>(_categories);
        Documents = new ReadOnlyObservableCollection<MasterDocumentRow>(_rows);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        UploadCommand = new AsyncRelayCommand(UploadAsync, () => !IsLoading && CanUpload);
        ReplaceCommand = new AsyncRelayCommand<MasterDocumentRow>(ReplaceAsync);
        EditCommand = new AsyncRelayCommand<MasterDocumentRow>(EditAsync);
        OpenCommand = new AsyncRelayCommand<MasterDocumentRow>(OpenAsync);
        OpenFolderCommand = new AsyncRelayCommand<MasterDocumentRow>(OpenFolderAsync);
        ArchiveCommand = new AsyncRelayCommand<MasterDocumentRow>(ArchiveAsync);
        RestoreCommand = new AsyncRelayCommand<MasterDocumentRow>(RestoreAsync);
        ToggleActiveCommand = new AsyncRelayCommand<MasterDocumentRow>(ToggleActiveAsync);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand UploadCommand { get; }

    public IAsyncRelayCommand<MasterDocumentRow> ReplaceCommand { get; }

    public IAsyncRelayCommand<MasterDocumentRow> EditCommand { get; }

    public IAsyncRelayCommand<MasterDocumentRow> OpenCommand { get; }

    public IAsyncRelayCommand<MasterDocumentRow> OpenFolderCommand { get; }

    public IAsyncRelayCommand<MasterDocumentRow> ArchiveCommand { get; }

    public IAsyncRelayCommand<MasterDocumentRow> RestoreCommand { get; }

    public IAsyncRelayCommand<MasterDocumentRow> ToggleActiveCommand { get; }

    public ReadOnlyObservableCollection<string> Categories { get; }

    public ReadOnlyObservableCollection<MasterDocumentRow> Documents { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                UploadCommand.NotifyCanExecuteChanged();
                ApplyFilter();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string MasterRootPath
    {
        get => _masterRootPath;
        private set => SetProperty(ref _masterRootPath, value);
    }

    public bool HasDocuments => _rows.Count > 0;

    public string EmptyStateMessage =>
        "Belum ada master dokumen. Pilih kategori, lalu unggah file master pertama.";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                UploadCommand.NotifyCanExecuteChanged();
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

    private bool CanUpload =>
        !string.Equals(SelectedCategory, AllCategoriesText, StringComparison.Ordinal)
        && _allCategories.Any(category => category.IsActive && string.Equals(category.Name, SelectedCategory, StringComparison.Ordinal));

    public Task LoadAsync() => ExecuteAsync(LoadInternalAsync);

    private async Task UploadAsync()
    {
        var category = _allCategories.FirstOrDefault(item =>
            item.IsActive && string.Equals(item.Name, SelectedCategory, StringComparison.Ordinal));
        if (category is null)
        {
            SetStatus("Pilih kategori aktif terlebih dahulu.", false);
            return;
        }

        var sourcePath = _filePickerService.PickMasterFile();
        if (sourcePath is null)
        {
            return;
        }

        var suggestedName = Path.GetFileNameWithoutExtension(sourcePath);
        var result = _dialogService.Show(suggestedName, false, true);
        if (result is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var document = await _documentStore.UploadAsync(
                category.Id,
                sourcePath,
                result.DisplayName,
                result.IsRequired);
            await LoadInternalAsync();
            SetStatus($"Master '{document.DisplayName}' berhasil diunggah.", true);
        });
    }

    private async Task ReplaceAsync(MasterDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_confirmationService.ConfirmReplaceMaster(row.Document.DisplayName))
        {
            return;
        }

        var sourcePath = _filePickerService.PickMasterFile();
        if (sourcePath is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var document = await _documentStore.ReplaceVersionAsync(row.Document.Id, sourcePath);
            await LoadInternalAsync();
            SetStatus($"'{document.DisplayName}' diperbarui ke versi {document.CurrentVersionNumber}.", true);
        });
    }

    private async Task EditAsync(MasterDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        var result = _dialogService.Show(row.Document.DisplayName, row.Document.IsRequired, false);
        if (result is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _documentStore.UpdateMetadataAsync(
                row.Document.Id,
                result.DisplayName,
                result.IsRequired);
            await LoadInternalAsync();
            SetStatus($"Metadata '{result.DisplayName}' diperbarui.", true);
        });
    }

    private async Task OpenAsync(MasterDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var path = await _documentStore.ResolveOpenPathAsync(row.Document.Id);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            SetStatus($"Membuka '{row.Document.DisplayName}'.", true);
        });
    }

    private async Task OpenFolderAsync(MasterDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var path = await _documentStore.ResolveOpenPathAsync(row.Document.Id);
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
            SetStatus($"Folder '{row.Document.DisplayName}' dibuka.", true);
        });
    }

    private async Task ArchiveAsync(MasterDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!_confirmationService.ConfirmArchiveMaster(row.Document.DisplayName))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _documentStore.SetArchivedAsync(row.Document.Id, true);
            await LoadInternalAsync();
            SetStatus($"Master '{row.Document.DisplayName}' diarsipkan.", true);
        });
    }

    private async Task RestoreAsync(MasterDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _documentStore.RestoreAsync(row.Document.Id);
            await LoadInternalAsync();
            SetStatus($"Master '{row.Document.DisplayName}' dikembalikan.", true);
        });
    }

    private async Task ToggleActiveAsync(MasterDocumentRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.Document.IsActive && !_confirmationService.ConfirmDeactivateMaster(row.Document.DisplayName))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            var next = !row.Document.IsActive;
            await _documentStore.SetActiveAsync(row.Document.Id, next);
            await LoadInternalAsync();
            SetStatus(
                next
                    ? $"Master '{row.Document.DisplayName}' diaktifkan."
                    : $"Master '{row.Document.DisplayName}' dinonaktifkan.",
                true);
        });
    }

    private async Task LoadInternalAsync()
    {
        var previousCategory = SelectedCategory;
        var categories = await _categoryStore.GetAllAsync();
        var documents = await _documentStore.GetAllAsync();
        var paths = await _pathService.GetAsync();

        _allCategories = categories;
        _allDocuments = documents;
        MasterRootPath = paths.MasterDocumentsDirectory;

        _categories.Clear();
        _categories.Add(AllCategoriesText);
        foreach (var category in categories.Where(item => item.IsActive))
        {
            _categories.Add(category.Name);
        }

        SelectedCategory = _categories.Contains(previousCategory) ? previousCategory : AllCategoriesText;
        ApplyFilter();
        SetStatus($"{documents.Count} master dokumen tersimpan.", true);
    }

    private void ApplyFilter()
    {
        _rows.Clear();
        var search = SearchText.Trim();

        foreach (var document in _allDocuments)
        {
            if (!string.Equals(SelectedCategory, AllCategoriesText, StringComparison.Ordinal)
                && !string.Equals(CategoryNameOf(document), SelectedCategory, StringComparison.Ordinal))
            {
                continue;
            }

            var categoryName = CategoryNameOf(document);
            if (!string.IsNullOrWhiteSpace(search)
                && document.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                && document.SourceFileName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                && categoryName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            _rows.Add(new MasterDocumentRow(
                document,
                categoryName,
                $"v{document.CurrentVersionNumber}",
                FormatSize(document.FileSizeBytes),
                StatusOf(document)));
        }

        OnPropertyChanged(nameof(HasDocuments));
    }

    private string CategoryNameOf(MasterDocumentInfo document) =>
        _allCategories.FirstOrDefault(category => category.Id == document.CategoryId)?.Name ?? "-";

    private static string StatusOf(MasterDocumentInfo document)
    {
        if (document.IsArchived)
        {
            return "Arsip";
        }

        if (!document.IsActive)
        {
            return "Nonaktif";
        }

        return document.IsRequired ? "Wajib" : "Opsional";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        IsLoading = true;
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
            IsLoading = false;
        }
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
