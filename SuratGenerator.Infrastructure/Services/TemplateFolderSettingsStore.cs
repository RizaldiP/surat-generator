using System.Text.Json;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Infrastructure.Services;

internal sealed class TemplateFolderSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TemplateFolderSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? AppPaths.TemplateFolderSettingsPath;
    }

    public async Task<TemplateFolderSettingsInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return Map(await ReadCoreAsync(cancellationToken));
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<TemplateFolderSettingsInfo> SetFolderAsync(
        string folderPath,
        bool includeSubfolders,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            data =>
            {
                data.FolderPath = folderPath;
                data.IncludeSubfolders = includeSubfolders;
            },
            cancellationToken);

    public Task<TemplateFolderSettingsInfo> SetLastScanAsync(
        DateTimeOffset value,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(data => data.LastScanAt = value, cancellationToken);

    private async Task<TemplateFolderSettingsInfo> UpdateAsync(
        Action<TemplateFolderSettingsData> update,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadCoreAsync(cancellationToken);
            update(data);
            await WriteCoreAsync(data, cancellationToken);
            return Map(data);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static TemplateFolderSettingsInfo Map(TemplateFolderSettingsData data) =>
        new(data.FolderPath ?? string.Empty, data.IncludeSubfolders, data.LastScanAt);

    private async Task<TemplateFolderSettingsData> ReadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return new TemplateFolderSettingsData();
        }

        var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken);
        return JsonSerializer.Deserialize<TemplateFolderSettingsData>(json, JsonOptions)
            ?? new TemplateFolderSettingsData();
    }

    private async Task WriteCoreAsync(TemplateFolderSettingsData data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json, cancellationToken);
    }

    private sealed class TemplateFolderSettingsData
    {
        public string? FolderPath { get; set; }

        public bool IncludeSubfolders { get; set; }

        public DateTimeOffset? LastScanAt { get; set; }
    }
}
