using System.Text.Json;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Infrastructure.Services;

internal sealed class WorkspaceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WorkspaceSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? AppPaths.WorkspaceSettingsPath;
    }

    public async Task<WorkspacePathsInfo?> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
            var data = JsonSerializer.Deserialize<WorkspacePathsData>(json, JsonOptions);
            if (data is null
                || string.IsNullOrWhiteSpace(data.MasterDocumentsDirectory)
                || string.IsNullOrWhiteSpace(data.ProjectsDirectory)
                || string.IsNullOrWhiteSpace(data.BackupsDirectory))
            {
                return null;
            }

            return new WorkspacePathsInfo(
                data.MasterDocumentsDirectory,
                data.ProjectsDirectory,
                data.BackupsDirectory);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(WorkspacePathsInfo paths, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = new WorkspacePathsData
            {
                MasterDocumentsDirectory = paths.MasterDocumentsDirectory,
                ProjectsDirectory = paths.ProjectsDirectory,
                BackupsDirectory = paths.BackupsDirectory
            };
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var temporaryPath = $"{_settingsPath}.tmp";
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, _settingsPath, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class WorkspacePathsData
    {
        public string? MasterDocumentsDirectory { get; set; }

        public string? ProjectsDirectory { get; set; }

        public string? BackupsDirectory { get; set; }
    }
}
