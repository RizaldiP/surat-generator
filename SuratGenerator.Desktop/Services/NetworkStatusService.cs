using System.Net.NetworkInformation;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Desktop.Services;

internal sealed class NetworkStatusService : INetworkStatusProvider, IDisposable
{
    public bool IsOnline { get; private set; }

    public event EventHandler? AvailabilityChanged;

    public NetworkStatusService()
    {
        IsOnline = NetworkInterface.GetIsNetworkAvailable();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public string StatusText => IsOnline ? "Jaringan: Online" : "Jaringan: Offline";

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (IsOnline == e.IsAvailable)
        {
            return;
        }

        IsOnline = e.IsAvailable;
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }
}
