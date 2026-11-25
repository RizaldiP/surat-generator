using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Util.Store;

namespace SuratGenerator.Infrastructure.Services;

internal sealed class DpapiDataStore : IDataStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SuratGenerator.DriveToken.v1");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> GetAsync<T>(string key)
    {
        await _gate.WaitAsync();
        try
        {
            var values = await ReadAsync();
            return values.TryGetValue(key, out var value)
                ? value.Deserialize<T>() ?? default!
                : default!;
        }
        catch (CryptographicException)
        {
            return default!;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        await _gate.WaitAsync();
        try
        {
            var values = await ReadAsync();
            values[key] = JsonSerializer.SerializeToElement(value);
            await WriteAsync(values);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync<T>(string key)
    {
        await _gate.WaitAsync();
        try
        {
            var values = await ReadAsync();
            if (values.Remove(key))
            {
                await WriteAsync(values);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (File.Exists(AppPaths.DriveTokenPath))
            {
                File.Delete(AppPaths.DriveTokenPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public string GenerateKey(string userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)));

    private static async Task<Dictionary<string, JsonElement>> ReadAsync()
    {
        if (!File.Exists(AppPaths.DriveTokenPath))
        {
            return new Dictionary<string, JsonElement>();
        }

        var protectedBytes = await File.ReadAllBytesAsync(AppPaths.DriveTokenPath);
        var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? new Dictionary<string, JsonElement>();
    }

    private static async Task WriteAsync(Dictionary<string, JsonElement> values)
    {
        Directory.CreateDirectory(AppPaths.SecureDirectory);
        var json = JsonSerializer.Serialize(values);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(AppPaths.DriveTokenPath, bytes);
    }
}
