using System.Security.Cryptography;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Documents.Models;
using SuratGenerator.Documents.Services;

namespace SuratGenerator.Infrastructure.Services;

public sealed class TemplateFolderService : ITemplateFolderService
{
    private readonly ITemplateStore _templateStore;
    private readonly ITemplateReader _templateReader;
    private readonly ILegacyTemplateConverter? _converter;
    private readonly TemplateFolderSettingsStore _settingsStore;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly IAppLogger _logger;

    public TemplateFolderService(
        ITemplateStore templateStore,
        ITemplateReader templateReader,
        IAppLogger? logger = null)
        : this(templateStore, templateReader, AppPaths.TemplateFolderSettingsPath, null, logger)
    {
    }

    public TemplateFolderService(
        ITemplateStore templateStore,
        ITemplateReader templateReader,
        ILegacyTemplateConverter? converter,
        IAppLogger? logger = null)
        : this(templateStore, templateReader, AppPaths.TemplateFolderSettingsPath, converter, logger)
    {
    }

    public TemplateFolderService(
        ITemplateStore templateStore,
        ITemplateReader templateReader,
        string settingsPath,
        IAppLogger? logger = null)
        : this(templateStore, templateReader, settingsPath, null, logger)
    {
    }

    public TemplateFolderService(
        ITemplateStore templateStore,
        ITemplateReader templateReader,
        string settingsPath,
        ILegacyTemplateConverter? converter,
        IAppLogger? logger = null)
    {
        _templateStore = templateStore;
        _templateReader = templateReader;
        _converter = converter;
        _settingsStore = new TemplateFolderSettingsStore(settingsPath);
        _logger = logger ?? NullAppLogger.Instance;
    }

    public Task<TemplateFolderSettingsInfo> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        _settingsStore.GetAsync(cancellationToken);

    public async Task<TemplateFolderSettingsInfo> SetFolderAsync(
        string folderPath,
        bool includeSubfolders,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new InvalidOperationException("Pilih folder template terlebih dahulu.");
        }

        var normalized = Normalize(folderPath);
        if (!Directory.Exists(normalized))
        {
            throw new InvalidOperationException("Folder template tidak ditemukan.");
        }

        return await _settingsStore.SetFolderAsync(normalized, includeSubfolders, cancellationToken);
    }

    public async Task<TemplateFolderScanSummary> ScanAsync(
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _scanGate.WaitAsync(cancellationToken);
        try
        {
            return await ScanCoreAsync(progress, cancellationToken);
        }
        finally
        {
            _scanGate.Release();
        }
    }

    private async Task<TemplateFolderScanSummary> ScanCoreAsync(
        IProgress<SyncProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);
        if (!settings.HasFolder)
        {
            throw new InvalidOperationException("Folder template belum diatur.");
        }

        var root = Normalize(settings.FolderPath);
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException("Folder template tidak ditemukan.");
        }

        var sourcePaths = EnumerateTemplates(root, settings.IncludeSubfolders);
        var templates = (await _templateStore.GetAllAsync(cancellationToken)).ToList();
        var bySourcePath = new Dictionary<string, TemplateInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates.Where(template => template.HasSource))
        {
            bySourcePath[Normalize(template.SourceFilePath!)] = template;
        }

        var messages = new List<string>();
        var imported = 0;
        var updated = 0;
        var unchanged = 0;
        var errors = 0;

        for (var index = 0; index < sourcePaths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = sourcePaths[index];
            var fileName = Path.GetFileName(sourcePath);
            var isLegacy = IsLegacyDocument(sourcePath);
            string? convertedPath = null;
            Report(progress, index + 1, sourcePaths.Length, fileName);

            try
            {
                var checksum = ComputeChecksum(sourcePath);
                var modifiedAt = File.GetLastWriteTimeUtc(sourcePath);

                if (bySourcePath.TryGetValue(sourcePath, out var existing))
                {
                    if (Same(existing.SourceChecksum, checksum) && existing.HasFile)
                    {
                        unchanged++;
                        continue;
                    }

                    var prepare = await PrepareReadPathAsync(sourcePath, isLegacy, cancellationToken);
                    if (prepare.Error is not null)
                    {
                        errors++;
                        messages.Add($"Gagal membaca '{fileName}': {prepare.Error}");
                        _logger.Warning($"Gagal membaca template folder '{sourcePath}': {prepare.Error}");
                        continue;
                    }

                    convertedPath = isLegacy ? prepare.ReadPath : null;
                    var readResult = await ReadWithRetryAsync(convertedPath ?? sourcePath, cancellationToken);
                    if (!readResult.IsSuccess)
                    {
                        errors++;
                        messages.Add($"Gagal membaca '{fileName}': {readResult.Error}");
                        _logger.Warning($"Gagal membaca template folder '{sourcePath}': {readResult.Error}");
                        continue;
                    }

                    await _templateStore.ReplaceFileAsync(
                        existing.Id,
                        convertedPath ?? sourcePath,
                        readResult.Fields,
                        fileName,
                        cancellationToken);
                    await _templateStore.SetSourceAsync(existing.Id, sourcePath, checksum, modifiedAt, cancellationToken);
                    updated++;
                    messages.Add($"'{fileName}' diperbarui dari folder{(isLegacy ? " (dikonversi dari .doc)" : string.Empty)}.");
                    continue;
                }

                var moved = templates.FirstOrDefault(template =>
                    template.HasSource &&
                    !template.SourceExists &&
                    Same(template.SourceChecksum, checksum));
                if (moved is not null)
                {
                    await _templateStore.SetSourceAsync(moved.Id, sourcePath, checksum, modifiedAt, cancellationToken);
                    bySourcePath[sourcePath] = moved;
                    updated++;
                    messages.Add($"'{fileName}' dipetakan ulang ke template yang sudah ada.");
                    continue;
                }

                var newPrepare = await PrepareReadPathAsync(sourcePath, isLegacy, cancellationToken);
                if (newPrepare.Error is not null)
                {
                    errors++;
                    messages.Add($"Gagal membaca '{fileName}': {newPrepare.Error}");
                    _logger.Warning($"Gagal membaca template folder '{sourcePath}': {newPrepare.Error}");
                    continue;
                }

                convertedPath = isLegacy ? newPrepare.ReadPath : null;
                var newResult = await ReadWithRetryAsync(convertedPath ?? sourcePath, cancellationToken);
                if (!newResult.IsSuccess)
                {
                    errors++;
                    messages.Add($"Gagal membaca '{fileName}': {newResult.Error}");
                    _logger.Warning($"Gagal membaca template folder '{sourcePath}': {newResult.Error}");
                    continue;
                }

                var created = await _templateStore.SaveAsync(
                    convertedPath ?? sourcePath,
                    newResult.Fields,
                    cancellationToken);
                if (convertedPath is not null)
                {
                    await _templateStore.UpdateMetadataAsync(
                        created.Id,
                        Path.GetFileNameWithoutExtension(sourcePath),
                        "Umum",
                        "1.0",
                        cancellationToken);
                }

                await _templateStore.SetSourceAsync(created.Id, sourcePath, checksum, modifiedAt, cancellationToken);
                bySourcePath[sourcePath] = created;
                imported++;
                messages.Add($"'{fileName}' diimpor dari folder{(isLegacy ? " (dikonversi dari .doc)" : string.Empty)}.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException)
            {
                errors++;
                messages.Add($"Gagal memproses '{fileName}': {exception.Message}");
                _logger.Error($"Gagal memproses template folder '{sourcePath}'.", exception);
            }
            finally
            {
                CleanupConverted(convertedPath);
            }
        }

        var presentPaths = new HashSet<string>(sourcePaths, StringComparer.OrdinalIgnoreCase);
        var missing = templates.Count(template =>
            TemplateFolderScope.IsWithin(template, settings)
            && template.HasSource
            && !presentPaths.Contains(Normalize(template.SourceFilePath!)));
        var outside = templates.Count(template =>
            template.HasSource && !TemplateFolderScope.IsWithin(template, settings));

        progress?.Report(new SyncProgressInfo("Memindai", 100, "Selesai"));

        var lastScan = await _settingsStore.SetLastScanAsync(DateTimeOffset.UtcNow, cancellationToken);
        messages.Add(missing > 0
            ? $"Pindai selesai. {lastScan.LastScanText}. {missing} template tidak ditemukan di folder."
            : $"Pindai selesai. {lastScan.LastScanText}.");
        if (outside > 0)
        {
            messages.Add($"{outside} template dari folder lain tidak ditampilkan.");
        }

        return new TemplateFolderScanSummary(imported, updated, unchanged, missing, errors, messages.ToArray());
    }

    private static string[] EnumerateTemplates(string root, bool includeSubfolders)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            ReturnSpecialDirectories = false
        };

        try
        {
            return Directory.EnumerateFiles(root, "*.*", options)
                .Where(IsSupportedDocument)
                .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(Normalize)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Folder template tidak dapat dibaca.", exception);
        }
    }

    private async Task<(string? ReadPath, string? Error)> PrepareReadPathAsync(
        string sourcePath,
        bool isLegacy,
        CancellationToken cancellationToken)
    {
        if (!isLegacy)
        {
            return (sourcePath, null);
        }

        if (_converter is null)
        {
            return (null, "File .doc memerlukan Microsoft Word untuk dikonversi.");
        }

        var conversion = await _converter.ConvertToDocxAsync(sourcePath, cancellationToken);
        return conversion.IsSuccess && conversion.DocxPath is not null
            ? (conversion.DocxPath, null)
            : (null, conversion.Error ?? "Gagal mengonversi file .doc.");
    }

    private static bool IsSupportedDocument(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".doc", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyDocument(string path) =>
        Path.GetExtension(path).Equals(".doc", StringComparison.OrdinalIgnoreCase);

    private static void CleanupConverted(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (!string.IsNullOrWhiteSpace(directory)
                && Directory.Exists(directory)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private async Task<TemplateReadResult> ReadWithRetryAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        TemplateReadResult result = TemplateReadResult.Failure("Template tidak dapat dibaca.");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await _templateReader.ReadAsync(sourcePath, cancellationToken);
            if (result.IsSuccess || !IsLockFailure(result.Error))
            {
                return result;
            }

            await Task.Delay(150, cancellationToken);
        }

        return result;
    }

    private static bool IsLockFailure(string? error) =>
        string.Equals(error, "File template tidak dapat dibaca.", StringComparison.Ordinal);

    private static string ComputeChecksum(string path)
    {
        using var md5 = MD5.Create();
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(md5.ComputeHash(stream));
    }

    private static void Report(
        IProgress<SyncProgressInfo>? progress,
        int done,
        int total,
        string message)
    {
        var percent = total <= 0 ? 100 : (int)Math.Clamp(done * 100 / total, 0, 100);
        progress?.Report(new SyncProgressInfo("Memindai", percent, message));
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => Path.GetFullPath(path);
}
