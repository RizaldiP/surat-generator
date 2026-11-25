using System.Text.Json;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Infrastructure.Services;

internal sealed class DriveSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<DriveSettingsInfo> GetAsync(CancellationToken cancellationToken = default)
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

    public Task<DriveSettingsInfo> UpdateFolderAsync(
        string folderUrl,
        string folderId,
        string? folderName,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            data =>
            {
                data.FolderUrl = folderUrl;
                data.FolderId = folderId;
                data.FolderName = folderName;
            },
            cancellationToken);

    public Task<DriveSettingsInfo> SetAccountAsync(
        string? email,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(data => data.AccountEmail = email, cancellationToken);

    public Task<DriveSettingsInfo> SetLastSyncAsync(
        DateTimeOffset value,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(data => data.LastSyncAt = value, cancellationToken);

    private async Task<DriveSettingsInfo> UpdateAsync(
        Action<DriveSettingsData> update,
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

    private static DriveSettingsInfo Map(DriveSettingsData data) =>
        new(
            data.FolderUrl ?? string.Empty,
            data.FolderId ?? string.Empty,
            data.FolderName,
            data.AccountEmail,
            data.LastSyncAt);

    private static async Task<DriveSettingsData> ReadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.DriveSettingsPath))
        {
            return new DriveSettingsData();
        }

        var json = await File.ReadAllTextAsync(AppPaths.DriveSettingsPath, cancellationToken);
        return JsonSerializer.Deserialize<DriveSettingsData>(json, JsonOptions) ?? new DriveSettingsData();
    }

    private static async Task WriteCoreAsync(DriveSettingsData data, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(AppPaths.DriveSettingsPath, json, cancellationToken);
    }

    private sealed class DriveSettingsData
    {
        public string? FolderUrl { get; set; }

        public string? FolderId { get; set; }

        public string? FolderName { get; set; }

        public string? AccountEmail { get; set; }

        public DateTimeOffset? LastSyncAt { get; set; }
    }
}
