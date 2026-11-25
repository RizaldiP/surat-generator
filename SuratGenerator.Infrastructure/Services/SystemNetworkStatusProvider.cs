using System.Net.NetworkInformation;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Infrastructure.Services;

public sealed class SystemNetworkStatusProvider : INetworkStatusProvider
{
    public bool IsOnline => NetworkInterface.GetIsNetworkAvailable();
}
