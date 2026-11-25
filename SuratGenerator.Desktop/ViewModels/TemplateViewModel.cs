using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Messages;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class TemplateViewModel : ObservableObject
{
    private readonly ITemplateStore _templateStore;
    private readonly ITemplateEditDialogService _editDialogService;
    private readonly IConfirmationService _confirmationService;
    private readonly ITemplateFolderService _folderService;
    private readonly ObservableCollection<string> _categories = new();
    private readonly ObservableCollection<TemplateInfo> _visibleTemplates = new();
    private List<TemplateInfo> _allTemplates = new();
    private string _searchText = string.Empty;
    private string _selectedCategory = "Semua";
    private string _selectedSort = "Nama A-Z";
    private bool _showFavoritesOnly;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;

    public TemplateViewModel(
        ITemplateStore templateStore,
        ITemplateEditDialogService editDialogService,
        IConfirmationService confirmationService,
        ITemplateFolderService folderService)
    {
        _templateStore = templateStore;
        _editDialogService = editDialogService;
        _confirmationService = confirmationService;
        _folderService = folderService;

        VisibleTemplates = new ReadOnlyObservableCollection<TemplateInfo>(_visibleTemplates);
        Categories = _categories;
        SortOptions = new[] { "Nama A-Z", "Nama Z-A", "Terbaru", "Terlama" };
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync, () => !IsLoading);
        ToggleFavoriteCommand = new AsyncRelayCommand<TemplateInfo>(ToggleFavoriteAsync);
        EditMetadataCommand = new AsyncRelayCommand<TemplateInfo>(EditMetadataAsync);
        DeleteCommand = new AsyncRelayCommand<TemplateInfo>(DeleteAsync);

        WeakReferenceMessenger.Default.Register<TemplatesChangedMessage>(
            this,
            (_, message) => _ = LoadAsync(scanFolder: false, externalSummary: message.Summary));
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ScanFolderCommand { get; }

    public IAsyncRelayCommand<TemplateInfo> ToggleFavoriteCommand { get; }

    public IAsyncRelayCommand<TemplateInfo> EditMetadataCommand { get; }

    public IAsyncRelayCommand<TemplateInfo> DeleteCommand { get; }

    public ObservableCollection<string> Categories { get; }

    public IReadOnlyList<string> SortOptions { get; }

    public ReadOnlyObservableCollection<TemplateInfo> VisibleTemplates { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set
        {
            if (SetProperty(ref _showFavoritesOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool HasAnyTemplates => _allTemplates.Count > 0;

    public string EmptyStateMessage => _allTemplates.Count == 0
        ? "Atur folder template di Pengaturan, lalu tekan Pindai Folder."
        : ShowFavoritesOnly
            ? "Belum ada template favorit. Klik Jadikan Favorit pada template yang ingin dipin."
            : "Atur folder template di Pengaturan, lalu tekan Pindai Folder.";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ScanFolderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasTemplates => _visibleTemplates.Count > 0;

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

    public async Task RefreshAsync() => await LoadAsync(scanFolder: false);

    public async Task ScanFolderAsync() => await LoadAsync(scanFolder: true);

    private async Task<(List<TemplateInfo> Templates, int HiddenCount)> LoadScopedTemplatesAsync()
    {
        var settings = await _folderService.GetSettingsAsync();
        var allTemplates = await _templateStore.GetAllAsync();
        var templates = allTemplates
            .Where(template => TemplateFolderScope.IsWithin(template, settings))
            .ToList();
        return (templates, allTemplates.Count - templates.Count);
    }

    private async Task LoadAsync(bool scanFolder, TemplateFolderScanSummary? externalSummary = null)
    {
        IsLoading = true;
        try
        {
            string? scanMessage = externalSummary?.SummaryText;
            if (scanFolder)
            {
                var settings = await _folderService.GetSettingsAsync();
                if (settings.HasFolder)
                {
                    var scan = await _folderService.ScanAsync();
                    scanMessage = scan.SummaryText;
                }
                else
                {
                    scanMessage = "Folder template belum diatur. Buka Pengaturan untuk memilih folder.";
                }
            }

            var (templates, hiddenCount) = await LoadScopedTemplatesAsync();
            _allTemplates = templates;
            var categories = _allTemplates
                .Select(template => template.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            _categories.Clear();
            _categories.Add("Semua");
            foreach (var category in categories)
            {
                _categories.Add(category);
            }

            if (!_categories.Contains(SelectedCategory))
            {
                _selectedCategory = "Semua";
                OnPropertyChanged(nameof(SelectedCategory));
            }

            ApplyFilters();
            OnPropertyChanged(nameof(HasAnyTemplates));
            OnPropertyChanged(nameof(EmptyStateMessage));
            var hiddenText = hiddenCount > 0
                ? $" {hiddenCount} template dari folder lain disembunyikan."
                : string.Empty;
            var listMessage = ShowFavoritesOnly
                ? $"Menampilkan {_visibleTemplates.Count} favorit dari {_allTemplates.Count} template.{hiddenText}"
                : $"Menampilkan {_visibleTemplates.Count} dari {_allTemplates.Count} template.{hiddenText}";
            SetStatus(
                scanMessage is null ? listMessage : $"{scanMessage} {listMessage}",
                externalSummary is null || !externalSummary.HasIssues);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal memuat template: {ex.Message}", false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleFavoriteAsync(TemplateInfo? template)
    {
        if (template is null)
        {
            return;
        }

        try
        {
            var next = !template.IsFavorite;
            await _templateStore.SetFavoriteAsync(template.Id, next);
            ReplaceTemplate(template with { IsFavorite = next });
            if (ShowFavoritesOnly)
            {
                ApplyFilters();
                OnPropertyChanged(nameof(EmptyStateMessage));
            }

            SetStatus(next ? "Template ditandai sebagai favorit." : "Tanda favorit dihapus.", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal memperbarui favorit: {ex.Message}", false);
        }
    }

    private async Task EditMetadataAsync(TemplateInfo? template)
    {
        if (template is null)
        {
            return;
        }

        var result = _editDialogService.Show(template);
        if (result is null)
        {
            return;
        }

        try
        {
            await _templateStore.UpdateMetadataAsync(
                template.Id,
                result.Name,
                result.Category,
                result.Version);
            await RefreshAsync();
            SetStatus("Metadata template berhasil diperbarui.", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal memperbarui metadata: {ex.Message}", false);
        }
    }

    private async Task DeleteAsync(TemplateInfo? template)
    {
        if (template is null || !_confirmationService.ConfirmDeleteTemplate(template.Name))
        {
            return;
        }

        try
        {
            await _templateStore.DeleteAsync(template.Id);
            await RefreshAsync();
            SetStatus($"Salinan lokal template '{template.Name}' dihapus.", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal menghapus template: {ex.Message}", false);
        }
    }

    private void ApplyFilters()
    {
        var results = TemplateQueryService.Query(
            _allTemplates,
            SearchText,
            SelectedCategory,
            CurrentSort,
            ShowFavoritesOnly);

        _visibleTemplates.Clear();
        foreach (var result in results)
        {
            _visibleTemplates.Add(result);
        }

        OnPropertyChanged(nameof(HasTemplates));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private TemplateSort CurrentSort => SelectedSort switch
    {
        "Nama Z-A" => TemplateSort.NameDescending,
        "Terbaru" => TemplateSort.Newest,
        "Terlama" => TemplateSort.Oldest,
        _ => TemplateSort.NameAscending
    };

    private void ReplaceTemplate(TemplateInfo updated)
    {
        var allIndex = _allTemplates.FindIndex(template => template.Id == updated.Id);
        if (allIndex >= 0)
        {
            _allTemplates[allIndex] = updated;
        }

        for (var index = 0; index < _visibleTemplates.Count; index++)
        {
            if (_visibleTemplates[index].Id == updated.Id)
            {
                _visibleTemplates[index] = updated;
                break;
            }
        }
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        if (_templateStore is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
