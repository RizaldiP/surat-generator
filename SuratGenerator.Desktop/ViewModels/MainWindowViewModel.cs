using CommunityToolkit.Mvvm.ComponentModel;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly NetworkStatusService _networkStatusService;
    private bool _isOnline;
    private string _networkStatusText = string.Empty;

    public MainWindowViewModel()
    {
        _networkStatusService = new NetworkStatusService();
        _networkStatusService.AvailabilityChanged += OnAvailabilityChanged;
        Refresh();
    }

    public bool IsOnline
    {
        get => _isOnline;
        private set => SetProperty(ref _isOnline, value);
    }

    public string NetworkStatusText
    {
        get => _networkStatusText;
        private set => SetProperty(ref _networkStatusText, value);
    }

    public void Dispose()
    {
        _networkStatusService.AvailabilityChanged -= OnAvailabilityChanged;
        _networkStatusService.Dispose();
    }

    private void OnAvailabilityChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        IsOnline = _networkStatusService.IsOnline;
        NetworkStatusText = _networkStatusService.StatusText;
    }
}
