using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Messages;

namespace SuratGenerator.Desktop.Services;

internal sealed class TemplateFolderWatcher : ITemplateFolderWatcher
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

    private readonly ITemplateFolderService _folderService;
    private readonly IAppLogger _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly object _sync = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounce;
    private string? _folderPath;
    private bool _includeSubfolders;
    private bool _disposed;

    public TemplateFolderWatcher(ITemplateFolderService folderService, IAppLogger logger)
    {
        _folderService = folderService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        ReconfigureAsync(cancellationToken);

    public async Task ReconfigureAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var settings = await _folderService.GetSettingsAsync(cancellationToken);
        if (!settings.HasFolder
            || string.IsNullOrWhiteSpace(settings.FolderPath)
            || !Directory.Exists(settings.FolderPath))
        {
            Detach();
            return;
        }

        var root = Path.GetFullPath(settings.FolderPath);
        lock (_sync)
        {
            if (string.Equals(_folderPath, root, StringComparison.OrdinalIgnoreCase)
                && _includeSubfolders == settings.IncludeSubfolders
                && _watcher is not null)
            {
                return;
            }
        }

        Detach();

        var watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = settings.IncludeSubfolders,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName,
            Filter = "*.*"
        };
        watcher.Created += OnChanged;
        watcher.Changed += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnRenamed;

        lock (_sync)
        {
            _folderPath = root;
            _includeSubfolders = settings.IncludeSubfolders;
            _watcher = watcher;
        }

        watcher.EnableRaisingEvents = true;
        _logger.Info($"Pemantauan folder template aktif: {root}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Detach();
        _lifetime.Cancel();
        _lifetime.Dispose();
        _scanGate.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs args)
    {
        if (!IsIgnored(args.FullPath))
        {
            ScheduleScan();
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        if (!IsIgnored(args.OldFullPath) || !IsIgnored(args.FullPath))
        {
            ScheduleScan();
        }
    }

    private static bool IsIgnored(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("~$", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(name);
        return !extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
    }

    private void ScheduleScan()
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource cancellation;
        lock (_sync)
        {
            _debounce?.Cancel();
            _debounce?.Dispose();
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _debounce = cancellation;
        }

        _ = ScanAfterDebounceAsync(cancellation);
    }

    private async Task ScanAfterDebounceAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(Debounce, cancellation.Token);
            await _scanGate.WaitAsync(cancellation.Token);
            try
            {
                var summary = await _folderService.ScanAsync(cancellationToken: _lifetime.Token);
                Application.Current?.Dispatcher.Invoke(
                    () => WeakReferenceMessenger.Default.Send(new TemplatesChangedMessage(summary)));
            }
            finally
            {
                _scanGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Error("Gagal memindai folder template otomatis.", exception);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_debounce, cancellation))
                {
                    _debounce = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void Detach()
    {
        FileSystemWatcher? watcher;
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            watcher = _watcher;
            cancellation = _debounce;
            _watcher = null;
            _debounce = null;
            _folderPath = null;
        }

        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnChanged;
            watcher.Changed -= OnChanged;
            watcher.Deleted -= OnChanged;
            watcher.Renamed -= OnRenamed;
            watcher.Dispose();
        }

        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }
}
