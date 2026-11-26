using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class CatatanViewModel : ObservableObject
{
    private readonly INoteStore _noteStore;
    private readonly INoteItemStore _noteItemStore;
    private readonly INoteDialogService _dialogService;
    private readonly IConfirmationService _confirmationService;
    private readonly ObservableCollection<NoteInfo> _notes = new();
    private readonly ObservableCollection<NoteItemInfo> _items = new();
    private IReadOnlyList<NoteInfo> _allNotes = Array.Empty<NoteInfo>();
    private IReadOnlyList<NoteItemInfo> _allItems = Array.Empty<NoteItemInfo>();
    private NoteInfo? _selectedNote;
    private string _searchText = string.Empty;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;

    public CatatanViewModel(
        INoteStore noteStore,
        INoteItemStore noteItemStore,
        INoteDialogService dialogService,
        IConfirmationService confirmationService)
    {
        _noteStore = noteStore;
        _noteItemStore = noteItemStore;
        _dialogService = dialogService;
        _confirmationService = confirmationService;

        Notes = new ReadOnlyObservableCollection<NoteInfo>(_notes);
        Items = new ReadOnlyObservableCollection<NoteItemInfo>(_items);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        AddNoteCommand = new AsyncRelayCommand(AddNoteAsync, () => !IsLoading);
        EditNoteCommand = new AsyncRelayCommand<NoteInfo>(EditNoteAsync);
        DeleteNoteCommand = new AsyncRelayCommand<NoteInfo>(DeleteNoteAsync);
        MoveNoteUpCommand = new AsyncRelayCommand<NoteInfo>(note => MoveNoteAsync(note, -1), _ => CanReorderNotes);
        MoveNoteDownCommand = new AsyncRelayCommand<NoteInfo>(note => MoveNoteAsync(note, 1), _ => CanReorderNotes);
        AddItemCommand = new AsyncRelayCommand(AddItemAsync, () => CanManageItems);
        EditItemCommand = new AsyncRelayCommand<NoteItemInfo>(EditItemAsync);
        DeleteItemCommand = new AsyncRelayCommand<NoteItemInfo>(DeleteItemAsync);
        MoveItemUpCommand = new AsyncRelayCommand<NoteItemInfo>(item => MoveItemAsync(item, -1), _ => CanReorderItems);
        MoveItemDownCommand = new AsyncRelayCommand<NoteItemInfo>(item => MoveItemAsync(item, 1), _ => CanReorderItems);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddNoteCommand { get; }

    public IAsyncRelayCommand<NoteInfo> EditNoteCommand { get; }

    public IAsyncRelayCommand<NoteInfo> DeleteNoteCommand { get; }

    public IAsyncRelayCommand<NoteInfo> MoveNoteUpCommand { get; }

    public IAsyncRelayCommand<NoteInfo> MoveNoteDownCommand { get; }

    public IAsyncRelayCommand AddItemCommand { get; }

    public IAsyncRelayCommand<NoteItemInfo> EditItemCommand { get; }

    public IAsyncRelayCommand<NoteItemInfo> DeleteItemCommand { get; }

    public IAsyncRelayCommand<NoteItemInfo> MoveItemUpCommand { get; }

    public IAsyncRelayCommand<NoteItemInfo> MoveItemDownCommand { get; }

    public IRelayCommand ClearSearchCommand { get; }

    public ReadOnlyObservableCollection<NoteInfo> Notes { get; }

    public ReadOnlyObservableCollection<NoteItemInfo> Items { get; }

    public NoteInfo? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (!SetProperty(ref _selectedNote, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanManageItems));
            OnPropertyChanged(nameof(CanReorderItems));
            OnPropertyChanged(nameof(ItemTitle));
            OnPropertyChanged(nameof(HasItems));
            AddItemCommand.NotifyCanExecuteChanged();
            MoveItemUpCommand.NotifyCanExecuteChanged();
            MoveItemDownCommand.NotifyCanExecuteChanged();
            ShowItems();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsFiltered));
            OnPropertyChanged(nameof(CanReorderNotes));
            OnPropertyChanged(nameof(CanReorderItems));
            MoveNoteUpCommand.NotifyCanExecuteChanged();
            MoveNoteDownCommand.NotifyCanExecuteChanged();
            MoveItemUpCommand.NotifyCanExecuteChanged();
            MoveItemDownCommand.NotifyCanExecuteChanged();
            ApplyFilter();
        }
    }

    public bool IsFiltered => !string.IsNullOrWhiteSpace(SearchText);

    public bool CanManageItems => SelectedNote is not null && !IsLoading;

    public bool CanReorderNotes => !IsLoading && !IsFiltered && _notes.Count > 1;

    public bool CanReorderItems => CanManageItems && !IsFiltered && _items.Count > 1;

    public string ItemTitle => SelectedNote is null
        ? "Pilih catatan untuk melihat syaratnya."
        : $"Syarat — {SelectedNote.Title}";

    public bool HasNotes => _notes.Count > 0;

    public bool HasItems => _items.Count > 0;

    public string EmptyStateMessage => IsFiltered
        ? "Tidak ada catatan atau syarat yang cocok dengan pencarian."
        : "Belum ada catatan. Tambahkan catatan untuk mencatat syarat pembuatan surat.";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
            {
                return;
            }

            RefreshCommand.NotifyCanExecuteChanged();
            AddNoteCommand.NotifyCanExecuteChanged();
            AddItemCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanManageItems));
            OnPropertyChanged(nameof(CanReorderNotes));
            OnPropertyChanged(nameof(CanReorderItems));
            MoveNoteUpCommand.NotifyCanExecuteChanged();
            MoveNoteDownCommand.NotifyCanExecuteChanged();
            MoveItemUpCommand.NotifyCanExecuteChanged();
            MoveItemDownCommand.NotifyCanExecuteChanged();
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

    public async Task LoadAsync() => await LoadCoreAsync();

    private async Task<bool> LoadCoreAsync()
    {
        IsLoading = true;
        try
        {
            _allNotes = await _noteStore.GetAllAsync();
            _allItems = await _noteItemStore.GetAllAsync();

            var selectedId = SelectedNote?.Id;
            ApplyFilter();
            SelectedNote = _notes.FirstOrDefault(note => note.Id == selectedId)
                ?? _notes.FirstOrDefault();
            ShowItems();

            SetStatus($"{_allNotes.Count} catatan, {_allItems.Count} syarat tersimpan.", true);
            return true;
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memuat catatan: {exception.Message}", false);
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = (SearchText ?? string.Empty).Trim();
        _notes.Clear();
        foreach (var note in _allNotes)
        {
            if (!IsFiltered || MatchesNote(note, query))
            {
                _notes.Add(note);
            }
        }

        if (SelectedNote is not null && !_notes.Any(note => note.Id == SelectedNote.Id))
        {
            _selectedNote = null;
            OnPropertyChanged(nameof(SelectedNote));
            OnPropertyChanged(nameof(CanManageItems));
            ShowItems();
        }

        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(CanReorderNotes));
        MoveNoteUpCommand.NotifyCanExecuteChanged();
        MoveNoteDownCommand.NotifyCanExecuteChanged();
    }

    private void ShowItems()
    {
        _items.Clear();
        if (SelectedNote is null)
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(CanReorderItems));
            return;
        }

        var query = (SearchText ?? string.Empty).Trim();
        var showAll = !IsFiltered
            || SelectedNote.Title.Contains(query, StringComparison.OrdinalIgnoreCase);
        foreach (var item in _allItems.Where(item => item.NoteId == SelectedNote.Id))
        {
            if (showAll || MatchesItem(item, query))
            {
                _items.Add(item);
            }
        }

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CanReorderItems));
        MoveItemUpCommand.NotifyCanExecuteChanged();
        MoveItemDownCommand.NotifyCanExecuteChanged();
    }

    private bool MatchesNote(NoteInfo note, string query)
    {
        if (note.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _allItems.Any(item =>
            item.NoteId == note.Id && MatchesItem(item, query));
    }

    private static bool MatchesItem(NoteItemInfo item, string query) =>
        item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Description.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task AddNoteAsync()
    {
        var result = _dialogService.ShowNote(null);
        if (result is null)
        {
            return;
        }

        try
        {
            await _noteStore.AddAsync(result.Title);
            if (await LoadCoreAsync())
            {
                SetStatus($"Catatan '{result.Title}' ditambahkan.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menambah catatan: {exception.Message}", false);
        }
    }

    private async Task EditNoteAsync(NoteInfo? note)
    {
        if (note is null)
        {
            return;
        }

        var result = _dialogService.ShowNote(note.Title);
        if (result is null)
        {
            return;
        }

        try
        {
            await _noteStore.RenameAsync(note.Id, result.Title);
            if (await LoadCoreAsync())
            {
                SetStatus($"Catatan diubah menjadi '{result.Title}'.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal mengubah catatan: {exception.Message}", false);
        }
    }

    private async Task DeleteNoteAsync(NoteInfo? note)
    {
        if (note is null || !_confirmationService.ConfirmDeleteNote(note.Title))
        {
            return;
        }

        try
        {
            await _noteStore.DeleteAsync(note.Id);
            if (await LoadCoreAsync())
            {
                SetStatus($"Catatan '{note.Title}' beserta syaratnya dihapus.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menghapus catatan: {exception.Message}", false);
        }
    }

    private async Task MoveNoteAsync(NoteInfo? note, int offset)
    {
        if (note is null || !CanReorderNotes)
        {
            return;
        }

        var index = FindNoteIndex(note.Id);
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= _notes.Count)
        {
            return;
        }

        var ids = _notes.Select(item => item.Id).ToList();
        (ids[index], ids[targetIndex]) = (ids[targetIndex], ids[index]);

        try
        {
            await _noteStore.ReorderAsync(ids);
            if (await LoadCoreAsync())
            {
                SetStatus("Urutan catatan berhasil disimpan.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menyimpan urutan catatan: {exception.Message}", false);
        }
    }

    private async Task AddItemAsync()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var result = _dialogService.ShowItem(null, null);
        if (result is null)
        {
            return;
        }

        try
        {
            await _noteItemStore.AddAsync(SelectedNote.Id, result.DisplayName, result.Description);
            if (await LoadCoreAsync())
            {
                SetStatus($"Syarat '{result.DisplayName}' ditambahkan.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menambah syarat: {exception.Message}", false);
        }
    }

    private async Task EditItemAsync(NoteItemInfo? item)
    {
        if (item is null)
        {
            return;
        }

        var result = _dialogService.ShowItem(item.DisplayName, item.Description);
        if (result is null)
        {
            return;
        }

        try
        {
            await _noteItemStore.UpdateAsync(item.Id, result.DisplayName, result.Description);
            if (await LoadCoreAsync())
            {
                SetStatus($"Syarat diubah menjadi '{result.DisplayName}'.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal mengubah syarat: {exception.Message}", false);
        }
    }

    private async Task DeleteItemAsync(NoteItemInfo? item)
    {
        if (item is null || !_confirmationService.ConfirmDeleteNoteItem(item.DisplayName))
        {
            return;
        }

        try
        {
            await _noteItemStore.DeleteAsync(item.Id);
            if (await LoadCoreAsync())
            {
                SetStatus($"Syarat '{item.DisplayName}' dihapus.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menghapus syarat: {exception.Message}", false);
        }
    }

    private async Task MoveItemAsync(NoteItemInfo? item, int offset)
    {
        if (item is null || SelectedNote is null || !CanReorderItems)
        {
            return;
        }

        var index = FindItemIndex(item.Id);
        var targetIndex = index + offset;
        if (index < 0 || targetIndex < 0 || targetIndex >= _items.Count)
        {
            return;
        }

        var ids = _items.Select(entry => entry.Id).ToList();
        (ids[index], ids[targetIndex]) = (ids[targetIndex], ids[index]);

        try
        {
            await _noteItemStore.ReorderAsync(SelectedNote.Id, ids);
            if (await LoadCoreAsync())
            {
                SetStatus("Urutan syarat berhasil disimpan.", true);
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal menyimpan urutan syarat: {exception.Message}", false);
        }
    }

    private int FindNoteIndex(Guid id)
    {
        for (var index = 0; index < _notes.Count; index++)
        {
            if (_notes[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindItemIndex(Guid id)
    {
        for (var index = 0; index < _items.Count; index++)
        {
            if (_items[index].Id == id)
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
