using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Messages;

namespace SuratGenerator.Desktop.Services;

internal sealed class ProjectFolderWatcher : IProjectFolderWatcher
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    private readonly IWorkspacePathService _pathService;
    private readonly IAppLogger _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly object _sync = new();
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounce;
    private string? _rootPath;
    private string? _watchPath;
    private bool _disposed;

    public ProjectFolderWatcher(IWorkspacePathService pathService, IAppLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        ReconfigureAsync(cancellationToken);

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

    private async Task ReconfigureAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string root;
        try
        {
            var paths = await _pathService.GetAsync(cancellationToken);
            root = Path.GetFullPath(paths.ProjectsDirectory);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            _logger.Warning($"Gagal menentukan folder pekerjaan untuk dipantau. {exception.Message}");
            Detach();
            return;
        }

        var watchPath = FindExistingAncestor(root);
        if (watchPath is null)
        {
            Detach();
            return;
        }

        lock (_sync)
        {
            if (string.Equals(_rootPath, root, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_watchPath, watchPath, StringComparison.OrdinalIgnoreCase)
                && _watcher is not null)
            {
                return;
            }
        }

        Detach();

        FileSystemWatcher watcher;
        try
        {
            watcher = new FileSystemWatcher(watchPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName,
                Filter = "*.*"
            };
            watcher.Created += OnDirectoryChanged;
            watcher.Deleted += OnDirectoryChanged;
            watcher.Renamed += OnDirectoryRenamed;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            _logger.Warning($"Gagal memantau folder pekerjaan '{watchPath}'. {exception.Message}");
            return;
        }

        lock (_sync)
        {
            _rootPath = root;
            _watchPath = watchPath;
            _watcher = watcher;
        }

        watcher.EnableRaisingEvents = true;
        _logger.Info($"Pemantauan folder pekerjaan aktif: {root}");
    }

    private static string? FindExistingAncestor(string path)
    {
        var candidate = path;
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Path.GetDirectoryName(candidate);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            candidate = parent;
        }

        return null;
    }

    private bool IsProjectDirectoryEvent(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(_rootPath))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }

        if (string.Equals(fullPath, _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var separator = Path.DirectorySeparatorChar.ToString();
        var prefix = _rootPath.EndsWith(separator, StringComparison.Ordinal)
            ? _rootPath
            : _rootPath + separator;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private void OnDirectoryChanged(object sender, FileSystemEventArgs args)
    {
        if (IsProjectDirectoryEvent(args.FullPath))
        {
            ScheduleScan();
        }
    }

    private void OnDirectoryRenamed(object sender, RenamedEventArgs args)
    {
        if (IsProjectDirectoryEvent(args.OldFullPath) || IsProjectDirectoryEvent(args.FullPath))
        {
            ScheduleScan();
        }
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
                Application.Current?.Dispatcher.Invoke(
                    () => WeakReferenceMessenger.Default.Send(new ProjectsChangedMessage()));
                await ReconfigureAsync(_lifetime.Token);
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
            _logger.Error("Gagal memproses perubahan folder pekerjaan.", exception);
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
            _rootPath = null;
            _watchPath = null;
        }

        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnDirectoryChanged;
            watcher.Deleted -= OnDirectoryChanged;
            watcher.Renamed -= OnDirectoryRenamed;
            watcher.Dispose();
        }

        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }
}
