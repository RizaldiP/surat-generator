using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class MasterStructureViewModel : ObservableObject
{
    private readonly IDocumentCategoryStore _categoryStore;
    private readonly ICategoryChecklistItemStore _checklistItemStore;
    private readonly IMasterStructureDialogService _dialogService;
    private readonly IConfirmationService _confirmationService;
    private readonly ObservableCollection<DocumentCategoryInfo> _categories = new();
    private readonly ObservableCollection<CategoryChecklistItemInfo> _checklistItems = new();
    private bool _isLoading;
    private string _structureVersion = string.Empty;
    private int _activeCategoryCount;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;
    private DocumentCategoryInfo? _selectedCategory;

    public MasterStructureViewModel(
        IDocumentCategoryStore categoryStore,
        ICategoryChecklistItemStore checklistItemStore,
        IMasterStructureDialogService dialogService,
        IConfirmationService confirmationService)
    {
        _categoryStore = categoryStore;
        _checklistItemStore = checklistItemStore;
        _dialogService = dialogService;
        _confirmationService = confirmationService;

        Categories = new ReadOnlyObservableCollection<DocumentCategoryInfo>(_categories);
        ChecklistItems = new ReadOnlyObservableCollection<CategoryChecklistItemInfo>(_checklistItems);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        AddCommand = new AsyncRelayCommand(AddAsync, () => !IsLoading);
        EditCommand = new AsyncRelayCommand<DocumentCategoryInfo>(EditAsync);
        ToggleActiveCommand = new AsyncRelayCommand<DocumentCategoryInfo>(ToggleActiveAsync);
        DeleteCommand = new AsyncRelayCommand<DocumentCategoryInfo>(DeleteAsync);
        MoveUpCommand = new AsyncRelayCommand<DocumentCategoryInfo>(category => MoveAsync(category, -1));
        MoveDownCommand = new AsyncRelayCommand<DocumentCategoryInfo>(category => MoveAsync(category, 1));
        AddChecklistItemCommand = new AsyncRelayCommand(AddChecklistItemAsync, () => CanManageChecklist);
        EditChecklistItemCommand = new AsyncRelayCommand<CategoryChecklistItemInfo>(EditChecklistItemAsync);
        ToggleChecklistItemCommand = new AsyncRelayCommand<CategoryChecklistItemInfo>(ToggleChecklistItemAsync);
        DeleteChecklistItemCommand = new AsyncRelayCommand<CategoryChecklistItemInfo>(DeleteChecklistItemAsync);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddCommand { get; }

    public IAsyncRelayCommand<DocumentCategoryInfo> EditCommand { get; }

    public IAsyncRelayCommand<DocumentCategoryInfo> ToggleActiveCommand { get; }

    public IAsyncRelayCommand<DocumentCategoryInfo> DeleteCommand { get; }

    public IAsyncRelayCommand<DocumentCategoryInfo> MoveUpCommand { get; }

    public IAsyncRelayCommand<DocumentCategoryInfo> MoveDownCommand { get; }

    public IAsyncRelayCommand AddChecklistItemCommand { get; }

    public IAsyncRelayCommand<CategoryChecklistItemInfo> EditChecklistItemCommand { get; }

    public IAsyncRelayCommand<CategoryChecklistItemInfo> ToggleChecklistItemCommand { get; }

    public IAsyncRelayCommand<CategoryChecklistItemInfo> DeleteChecklistItemCommand { get; }

    public ReadOnlyObservableCollection<DocumentCategoryInfo> Categories { get; }

    public ReadOnlyObservableCollection<CategoryChecklistItemInfo> ChecklistItems { get; }

    public DocumentCategoryInfo? SelectedCategory
    {
        get => _selectedCategory;
        private set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(CanManageChecklist));
                OnPropertyChanged(nameof(ChecklistTitle));
                AddChecklistItemCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanManageChecklist => SelectedCategory is not null && !IsLoading;

    public string ChecklistTitle => SelectedCategory is null
        ? "Pilih kategori untuk mengatur item wajib."
        : $"Item wajib — {SelectedCategory.Name}";

    public string StructureVersion
    {
        get => _structureVersion;
        private set => SetProperty(ref _structureVersion, value);
    }

    public int ActiveCategoryCount
    {
        get => _activeCategoryCount;
        private set => SetProperty(ref _activeCategoryCount, value);
    }

    public bool HasCategories => _categories.Count > 0;

    public string EmptyStateMessage =>
        "Belum ada kategori aktif. Tambahkan kategori untuk menyusun folder master.";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                AddCommand.NotifyCanExecuteChanged();
                AddChecklistItemCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanManageChecklist));
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

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var snapshot = await _categoryStore.GetSnapshotAsync();
            var categories = await _categoryStore.GetAllAsync();

            var selectedId = SelectedCategory?.Id;
            _categories.Clear();
            foreach (var category in categories)
            {
                _categories.Add(category);
            }

            SelectedCategory = _categories.FirstOrDefault(category => category.Id == selectedId)
                ?? _categories.FirstOrDefault();
            await LoadChecklistItemsAsync();

            StructureVersion = snapshot.Version;
            ActiveCategoryCount = snapshot.Categories.Count;
            OnPropertyChanged(nameof(HasCategories));
            SetStatus(
                $"{categories.Count} kategori tersimpan, {snapshot.Categories.Count} kategori aktif.",
                true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memuat struktur folder: {exception.Message}", false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectCategoryAsync(DocumentCategoryInfo? category)
    {
        if (SelectedCategory?.Id == category?.Id)
        {
            return;
        }

        SelectedCategory = category;
        await LoadChecklistItemsAsync();
    }

    private async Task LoadChecklistItemsAsync()
    {
        _checklistItems.Clear();
        if (SelectedCategory is null)
        {
            return;
        }

        var items = await _checklistItemStore.GetByCategoryAsync(SelectedCategory.Id);
        foreach (var item in items)
        {
            _checklistItems.Add(item);
        }
    }

    private async Task AddChecklistItemAsync()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        var result = _dialogService.Show(null);
        if (result is null)
        {
            return;
        }

        try
        {
            await _checklistItemStore.AddAsync(SelectedCategory.Id, result.Name);
            await LoadChecklistItemsAsync();
            SetStatus($"Item wajib '{result.Name}' ditambahkan ke {SelectedCategory.Name}.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menambah item wajib: {exception.Message}", false);
        }
    }

    private async Task EditChecklistItemAsync(CategoryChecklistItemInfo? item)
    {
        if (item is null || SelectedCategory is null)
        {
            return;
        }

        var result = _dialogService.Show(item.DisplayName);
        if (result is null)
        {
            return;
        }

        try
        {
            await _checklistItemStore.RenameAsync(item.Id, result.Name);
            await LoadChecklistItemsAsync();
            SetStatus($"Item wajib diubah menjadi '{result.Name}'.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal mengubah item wajib: {exception.Message}", false);
        }
    }

    private async Task ToggleChecklistItemAsync(CategoryChecklistItemInfo? item)
    {
        if (item is null)
        {
            return;
        }

        var nextActive = !item.IsActive;
        if (!_confirmationService.ConfirmToggleChecklistItem(item.DisplayName, nextActive))
        {
            return;
        }

        try
        {
            await _checklistItemStore.SetActiveAsync(item.Id, nextActive);
            await LoadChecklistItemsAsync();
            SetStatus(
                nextActive
                    ? $"Item wajib '{item.DisplayName}' diaktifkan."
                    : $"Item wajib '{item.DisplayName}' dinonaktifkan.",
                true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memperbarui item wajib: {exception.Message}", false);
        }
    }

    private async Task DeleteChecklistItemAsync(CategoryChecklistItemInfo? item)
    {
        if (item is null)
        {
            return;
        }

        if (!_confirmationService.ConfirmDeleteChecklistItem(item.DisplayName))
        {
            return;
        }

        try
        {
            await _checklistItemStore.DeleteAsync(item.Id);
            await LoadChecklistItemsAsync();
            SetStatus($"Item wajib '{item.DisplayName}' dihapus.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menghapus item wajib: {exception.Message}", false);
        }
    }

    public async Task MoveCategoryAsync(Guid sourceId, Guid targetId)
    {
        if (sourceId == targetId || IsLoading)
        {
            return;
        }

        var sourceIndex = FindIndex(sourceId);
        var targetIndex = FindIndex(targetId);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        var ids = _categories.Select(category => category.Id).ToList();
        var draggedId = ids[sourceIndex];
        ids.RemoveAt(sourceIndex);
        ids.Insert(targetIndex, draggedId);

        await ApplyReorderAsync(ids);
    }

    private async Task AddAsync()
    {
        var result = _dialogService.Show(null);
        if (result is null)
        {
            return;
        }

        try
        {
            await _categoryStore.AddAsync(result.Name);
            await LoadAsync();
            SetStatus($"Kategori '{result.Name}' ditambahkan.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menambah kategori: {exception.Message}", false);
        }
    }

    private async Task EditAsync(DocumentCategoryInfo? category)
    {
        if (category is null)
        {
            return;
        }

        var result = _dialogService.Show(category.Name);
        if (result is null)
        {
            return;
        }

        try
        {
            await _categoryStore.RenameAsync(category.Id, result.Name);
            await LoadAsync();
            SetStatus($"Kategori diubah menjadi '{result.Name}'.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal mengubah kategori: {exception.Message}", false);
        }
    }

    private async Task ToggleActiveAsync(DocumentCategoryInfo? category)
    {
        if (category is null)
        {
            return;
        }

        if (category.IsActive && !_confirmationService.ConfirmDeactivateCategory(category.Name))
        {
            return;
        }

        try
        {
            var next = !category.IsActive;
            await _categoryStore.SetActiveAsync(category.Id, next);
            await LoadAsync();
            SetStatus(
                next ? $"Kategori '{category.Name}' diaktifkan." : $"Kategori '{category.Name}' dinonaktifkan.",
                true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memperbarui status kategori: {exception.Message}", false);
        }
    }

    private async Task DeleteAsync(DocumentCategoryInfo? category)
    {
        if (category is null || !_confirmationService.ConfirmDeleteCategory(category.Name))
        {
            return;
        }

        try
        {
            await _categoryStore.DeleteAsync(category.Id);
            await LoadAsync();
            SetStatus($"Kategori '{category.Name}' dihapus. Struktur proyek lama tidak berubah.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menghapus kategori: {exception.Message}", false);
        }
    }

    private async Task MoveAsync(DocumentCategoryInfo? category, int offset)
    {
        if (category is null)
        {
            return;
        }

        var index = FindIndex(category.Id);
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= _categories.Count)
        {
            return;
        }

        var ids = _categories.Select(item => item.Id).ToList();
        (ids[index], ids[targetIndex]) = (ids[targetIndex], ids[index]);
        await ApplyReorderAsync(ids);
    }

    private async Task ApplyReorderAsync(IReadOnlyList<Guid> ids)
    {
        try
        {
            await _categoryStore.ReorderAsync(ids);
            await LoadAsync();
            SetStatus("Urutan kategori berhasil disimpan.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menyimpan urutan kategori: {exception.Message}", false);
        }
    }

    private int FindIndex(Guid id)
    {
        for (var index = 0; index < _categories.Count; index++)
        {
            if (_categories[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
