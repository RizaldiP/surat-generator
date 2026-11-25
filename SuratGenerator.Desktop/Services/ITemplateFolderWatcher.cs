namespace SuratGenerator.Desktop.Services;

internal interface ITemplateFolderWatcher : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task ReconfigureAsync(CancellationToken cancellationToken = default);
}
