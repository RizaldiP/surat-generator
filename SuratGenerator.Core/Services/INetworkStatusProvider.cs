namespace SuratGenerator.Core.Services;

public interface INetworkStatusProvider
{
    bool IsOnline { get; }
}
