using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class RiwayatSuratViewModel : ObservableObject
{
    private readonly IGeneratedDocumentStore _generatedDocumentStore;
    private readonly ObservableCollection<GeneratedDocumentInfo> _documents = new();
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public RiwayatSuratViewModel(IGeneratedDocumentStore generatedDocumentStore)
    {
        _generatedDocumentStore = generatedDocumentStore;
        Documents = new ReadOnlyObservableCollection<GeneratedDocumentInfo>(_documents);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        OpenFileCommand = new RelayCommand<GeneratedDocumentInfo>(OpenFile);
        OpenFolderCommand = new RelayCommand<GeneratedDocumentInfo>(OpenFolder);
    }

    public ReadOnlyObservableCollection<GeneratedDocumentInfo> Documents { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand<GeneratedDocumentInfo> OpenFileCommand { get; }

    public IRelayCommand<GeneratedDocumentInfo> OpenFolderCommand { get; }

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

    public bool HasDocuments => _documents.Count > 0;

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
            var documents = await _generatedDocumentStore.GetAllAsync();
            _documents.Clear();
            foreach (var document in documents)
            {
                _documents.Add(document);
            }

            OnPropertyChanged(nameof(HasDocuments));
            if (_documents.Count == 0)
            {
                StatusMessage = "Belum ada surat yang diekspor atau dicetak.";
            }
        }
        catch (Exception)
        {
            StatusMessage = "Gagal memuat riwayat surat.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenFile(GeneratedDocumentInfo? document)
    {
        if (document is null || !document.HasFile || !File.Exists(document.FilePath))
        {
            StatusMessage = "File tidak ditemukan.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(document.FilePath) { UseShellExecute = true });
        }
        catch (Exception)
        {
            StatusMessage = "Gagal membuka file.";
        }
    }

    private void OpenFolder(GeneratedDocumentInfo? document)
    {
        if (document is null || !document.HasFile || !File.Exists(document.FilePath))
        {
            StatusMessage = "File tidak ditemukan.";
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add("/select,");
            startInfo.ArgumentList.Add(document.FilePath);
            Process.Start(startInfo);
        }
        catch (Exception)
        {
            StatusMessage = "Gagal membuka folder file.";
        }
    }
}
