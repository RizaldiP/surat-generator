using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.Views.Pages;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class DraftViewModel : ObservableObject
{
    private readonly IDraftStore _draftStore;
    private readonly INavigationService _navigationService;
    private readonly ObservableCollection<DraftInfo> _drafts = new();
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public DraftViewModel(IDraftStore draftStore, INavigationService navigationService)
    {
        _draftStore = draftStore;
        _navigationService = navigationService;
        Drafts = new ReadOnlyObservableCollection<DraftInfo>(_drafts);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        OpenDraftCommand = new RelayCommand<DraftInfo>(OpenDraft);
    }

    public ReadOnlyObservableCollection<DraftInfo> Drafts { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand<DraftInfo> OpenDraftCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasDrafts => _drafts.Count > 0;

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

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var drafts = await _draftStore.GetAllAsync();
            _drafts.Clear();
            foreach (var draft in drafts)
            {
                _drafts.Add(draft);
            }

            OnPropertyChanged(nameof(HasDrafts));
            if (_drafts.Count == 0)
            {
                StatusMessage = "Belum ada draft yang tersimpan.";
            }
        }
        catch (Exception exception)
        {
            App.Logger.Error("Gagal memuat daftar draft.", exception);
            StatusMessage = "Gagal memuat daftar draft.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenDraft(DraftInfo? draft)
    {
        if (draft is null)
        {
            return;
        }

        _navigationService.NavigateTo<BuatSuratPage>(draft.Id);
    }
}
