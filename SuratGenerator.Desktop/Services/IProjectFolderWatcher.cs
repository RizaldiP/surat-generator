using SuratGenerator.Core.Services;

namespace SuratGenerator.Desktop.Services;

internal interface IProjectFolderWatcher : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
