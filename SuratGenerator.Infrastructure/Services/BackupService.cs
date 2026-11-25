using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;

namespace SuratGenerator.Infrastructure.Services;

public sealed class BackupService : IBackupService, IDisposable
{
    private static readonly SemaphoreSlim OperationGate = new(1, 1);

    private const string ManifestEntryName = "manifest.json";
    private const string DatabaseEntryName = "database/surat-generator.db";
    private const long MaxArchiveBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxEntryBytes = 512L * 1024 * 1024;
    private const long MaxTotalBytes = 4L * 1024 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly DbContextOptions<SuratGeneratorDbContext> _options;
    private readonly IWorkspacePathService _pathService;
    private readonly IAppLogger _logger;
    private readonly Func<IBackupRecordStore> _recordStoreFactory;
    private readonly Lazy<IBackupRecordStore> _ownedRecordStore;
    private readonly BackupPaths _paths;

    public BackupService()
        : this(DatabaseOptionsFactory.CreateProduction())
    {
    }

    public BackupService(
        DbContextOptions<SuratGeneratorDbContext> options,
        IWorkspacePathService? pathService = null,
        IAppLogger? logger = null,
        Func<IBackupRecordStore>? recordStoreFactory = null,
        BackupPaths? paths = null)
    {
        _options = options;
        _paths = paths ?? BackupPaths.Production;
        _pathService = pathService ?? new WorkspacePathService(
            _paths.ConfigFilePaths.FirstOrDefault() ?? Path.Combine(Path.GetDirectoryName(_paths.DatabasePath)!, "workspace-paths.json"),
            Path.GetDirectoryName(_paths.DatabasePath)!);
        _logger = logger ?? NullAppLogger.Instance;
        _ownedRecordStore = new Lazy<IBackupRecordStore>(() => new BackupRecordStore(options, _logger));
        _recordStoreFactory = recordStoreFactory ?? (() => _ownedRecordStore.Value);
    }

    public void Dispose()
    {
        if (_ownedRecordStore.IsValueCreated)
        {
            (_ownedRecordStore.Value as IDisposable)?.Dispose();
        }
    }

    public async Task<BackupPreview> PreviewAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("File backup tidak ditemukan.", fullPath);
        }

        if (new FileInfo(fullPath).Length > MaxArchiveBytes)
        {
            throw new InvalidDataException("Ukuran arsip backup melebihi batas maksimum.");
        }

        BackupManifest manifest;
        HashSet<string> archiveEntries;
        await using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            var manifestEntry = zip.GetEntry(ManifestEntryName)
                ?? throw new InvalidDataException("Manifest backup tidak ditemukan.");
            await using var manifestStream = manifestEntry.Open();
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Manifest backup tidak valid.");

            archiveEntries = zip.Entries
                .Where(entry => entry.FullName != ManifestEntryName)
                .Select(entry => NormalizeEntryPath(entry.FullName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        ValidateManifest(manifest, archiveEntries);
        var workspace = await _pathService.GetAsync(cancellationToken);
        return BuildPreview(fullPath, manifest, archiveEntries, workspace);
    }

    public async Task<BackupResult> CreateAsync(
        BackupRequest request,
        IProgress<BackupProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var operationLock = await AcquireOperationLockAsync(cancellationToken);
        return await CreateCoreAsync(request, progress, cancellationToken);
    }

    private async Task<BackupResult> CreateCoreAsync(
        BackupRequest request,
        IProgress<BackupProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationDirectory))
        {
            throw new ArgumentException("Folder tujuan backup wajib diisi.", nameof(request));
        }

        var destinationRoot = Path.GetFullPath(request.DestinationDirectory);
        var workspace = await _pathService.GetAsync(cancellationToken);
        ValidateDestination(destinationRoot, workspace);
        Directory.CreateDirectory(destinationRoot);

        var sources = await BuildBackupSourcesAsync(request, workspace, destinationRoot, cancellationToken);
        var sourceFiles = sources.Files.ToList();
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var finalPath = Path.Combine(destinationRoot, $"{SanitizeKind(request.Kind)}-{stamp}-{Guid.NewGuid():N}.zip");
        var tempPath = finalPath + ".tmp";
        string? databaseSnapshotPath = null;

        try
        {
            var databaseIndex = sourceFiles.FindIndex(source => source.EntryPath == DatabaseEntryName);
            if (databaseIndex >= 0)
            {
                databaseSnapshotPath = Path.Combine(destinationRoot, $".db-snapshot-{Guid.NewGuid():N}.tmp");
                await CreateDatabaseSnapshotAsync(databaseSnapshotPath, cancellationToken);
                sourceFiles[databaseIndex] = sourceFiles[databaseIndex] with { SourcePath = databaseSnapshotPath };
            }

            Report(progress, "Arsip", "Menulis arsip backup.", 45);
            var entries = new List<BackupEntry>(sourceFiles.Count);
            BackupManifest? manifest = null;
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                for (var index = 0; index < sourceFiles.Count; index++)
                {
                    var entry = await AddFileToArchiveAsync(zip, sourceFiles[index], cancellationToken);
                    entries.Add(entry);
                    Report(progress, "Arsip", $"Memproses {index + 1} dari {sourceFiles.Count} file.", 45 + ((index + 1) * 40 / Math.Max(1, sourceFiles.Count)));
                }

                if (entries.Sum(entry => entry.SizeBytes) > MaxTotalBytes)
                {
                    throw new InvalidDataException("Total ukuran backup melebihi batas maksimum.");
                }

                manifest = new BackupManifest(
                    BackupFormats.Current,
                    DateTimeOffset.UtcNow,
                    request.Kind,
                    entries,
                    sources.Projects,
                    request.Notes);
                await WriteManifestAsync(zip, manifest, cancellationToken);
            }

            Report(progress, "Verifikasi", "Memverifikasi integritas arsip.", 85);
            var verified = await VerifyArchiveAsync(tempPath, manifest!, cancellationToken);
            if (!verified)
            {
                throw new InvalidDataException("Verifikasi integritas backup gagal.");
            }

            File.Move(tempPath, finalPath);
            var sizeBytes = new FileInfo(finalPath).Length;
            var record = await _recordStoreFactory().AddAsync(new BackupRecordInfo(
                Guid.NewGuid(),
                manifest!.CreatedAt,
                finalPath,
                request.Kind,
                BackupStatuses.Berhasil,
                true,
                entries.Count,
                sizeBytes,
                null), cancellationToken);

            Report(progress, "Selesai", "Backup berhasil dibuat.", 100);
            _logger.Info($"Backup {request.Kind} berhasil: {finalPath}");
            return new BackupResult(record, finalPath, manifest!);
        }
        catch (Exception exception)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (databaseSnapshotPath is not null && File.Exists(databaseSnapshotPath))
            {
                File.Delete(databaseSnapshotPath);
            }

            _logger.Error("Gagal membuat backup.", exception);
            try
            {
                await _recordStoreFactory().AddAsync(new BackupRecordInfo(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    finalPath,
                    request.Kind,
                    BackupStatuses.Gagal,
                    false,
                    0,
                    0,
                    exception.Message), CancellationToken.None);
            }
            catch (Exception recordException)
            {
                _logger.Error("Gagal menyimpan catatan backup gagal.", recordException);
            }

            throw;
        }
        finally
        {
            if (databaseSnapshotPath is not null && File.Exists(databaseSnapshotPath))
            {
                File.Delete(databaseSnapshotPath);
            }
        }
    }

    public async Task<BackupResult?> EnsureDailyBackupAsync(CancellationToken cancellationToken = default)
    {
        await using var operationLock = await AcquireOperationLockAsync(cancellationToken);
        var records = await _recordStoreFactory().GetAllAsync(cancellationToken);
        var threshold = DateTimeOffset.UtcNow.AddDays(-1);
        if (records.Any(record =>
                record.Status == BackupStatuses.Berhasil
                && record.IsVerified
                && !string.IsNullOrWhiteSpace(record.Location)
                && File.Exists(record.Location)
                && record.CreatedAt.UtcDateTime >= threshold.UtcDateTime))
        {
            return null;
        }

        var workspace = await _pathService.GetAsync(cancellationToken);
        return await CreateCoreAsync(
            new BackupRequest(
                workspace.BackupsDirectory,
                BackupKinds.Otomatis,
                true,
                Array.Empty<Guid>(),
                "Backup otomatis harian."),
            null,
            cancellationToken);
    }

    public async Task<RestoreResult> StageRestoreAsync(
        RestoreRequest request,
        IProgress<BackupProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var operationLock = await AcquireOperationLockAsync(cancellationToken);
        var preview = await PreviewAsync(request.ArchivePath, cancellationToken);
        if (!preview.CanRestore)
        {
            throw new InvalidOperationException("Arsip backup tidak dapat dipulihkan.");
        }

        if (File.Exists(_paths.PendingRestoreMarkerPath))
        {
            throw new InvalidOperationException("Masih ada restore yang menunggu restart. Selesaikan restore tersebut terlebih dahulu.");
        }

        var workspace = await _pathService.GetAsync(cancellationToken);
        Report(progress, "Pra-restore", "Membuat backup kondisi saat ini.", 5);
        var preRestoreBackup = await CreateCoreAsync(
            new BackupRequest(
                workspace.BackupsDirectory,
                BackupKinds.PraPulihkan,
                true,
                Array.Empty<Guid>(),
                "Backup otomatis sebelum restore."),
            progress,
            cancellationToken);

        var stagingRoot = Path.Combine(_paths.PendingRestoreDirectory, $"staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);

        try
        {
            Report(progress, "Restore", "Menyiapkan file restore.", 55);
            var selectedFolders = ResolveSelectedProjectFolders(request, preview.Manifest);
            var marker = new PendingRestoreMarker
            {
                ArchivePath = preview.ArchivePath,
                StagedDirectory = stagingRoot,
                CreatedAt = DateTimeOffset.UtcNow,
                PreRestoreBackupPath = preRestoreBackup.ArchivePath
            };

            await using var stream = new FileStream(preview.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var selectedEntries = preview.Manifest.Entries
                .Where(entry => ShouldRestore(entry.Path, request, selectedFolders))
                .ToArray();

            for (var index = 0; index < selectedEntries.Length; index++)
            {
                var expected = selectedEntries[index];
                var relativePath = NormalizeEntryPath(expected.Path);
                var zipEntry = zip.GetEntry(expected.Path)
                    ?? throw new InvalidDataException($"Isi backup tidak ditemukan: {expected.Path}");
                var stagedPath = Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var destinationPath = ResolveDestination(relativePath, workspace)
                    ?? throw new InvalidDataException($"Lokasi tujuan tidak dikenali: {expected.Path}");
                var hadExistingDestination = File.Exists(destinationPath);

                await ExtractAndVerifyAsync(zipEntry, stagedPath, expected, cancellationToken);
                marker.Entries.Add(new PendingRestoreEntry
                {
                    EntryPath = expected.Path,
                    StagedPath = stagedPath,
                    DestinationPath = destinationPath,
                    HadExistingDestination = hadExistingDestination,
                    Sha256 = expected.Sha256,
                    SizeBytes = expected.SizeBytes
                });

                Report(progress, "Restore", $"Menyiapkan {index + 1} dari {selectedEntries.Length} file.", 55 + ((index + 1) * 35 / Math.Max(1, selectedEntries.Length)));
            }

            await WriteMarkerAsync(marker, _paths.PendingRestoreMarkerPath, cancellationToken);
            ClearRestoreError();
            Report(progress, "Selesai", "Restore disiapkan. Restart aplikasi untuk menyelesaikan.", 100);
            _logger.Info($"Restore disiapkan dari {preview.ArchivePath}.");
            return new RestoreResult(
                true,
                "Backup kondisi saat ini dibuat. Restart aplikasi untuk menyelesaikan restore.",
                preview,
                preRestoreBackup.ArchivePath);
        }
        catch
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, true);
            }

            throw;
        }
    }

    public async Task<bool> FinalizePendingRestoreAsync(CancellationToken cancellationToken = default)
    {
        await using var operationLock = await AcquireOperationLockAsync(cancellationToken);
        if (!File.Exists(_paths.PendingRestoreMarkerPath))
        {
            CleanupOrphanedStaging();
            return false;
        }

        PendingRestoreMarker marker;
        await using (var stream = new FileStream(_paths.PendingRestoreMarkerPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
        {
            marker = await JsonSerializer.DeserializeAsync<PendingRestoreMarker>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Penanda restore tidak valid.");
        }

        var stagingRoot = Path.GetFullPath(marker.StagedDirectory);
        string? rollbackRoot = null;
        var hasDatabaseEntry = false;

        try
        {
            if (!PathSafety.IsWithin(_paths.PendingRestoreDirectory, stagingRoot, true))
            {
                throw new InvalidDataException("Lokasi file restore tidak valid.");
            }

            var workspace = await _pathService.GetAsync(cancellationToken);
            ValidateRestoreDestinations(marker, workspace);

            rollbackRoot = Path.Combine(_paths.PendingRestoreDirectory, $"rollback-{Guid.NewGuid():N}");
            Directory.CreateDirectory(rollbackRoot);
            hasDatabaseEntry = marker.Entries.Any(entry => IsDatabaseDestination(entry.DestinationPath));

            foreach (var entry in marker.Entries)
            {
                if (!PathSafety.IsWithin(stagingRoot, entry.StagedPath, false))
                {
                    throw new InvalidDataException($"File restore di luar folder staging: {entry.StagedPath}");
                }

                var fileInfo = new FileInfo(entry.StagedPath);
                if (!fileInfo.Exists || fileInfo.Length != entry.SizeBytes)
                {
                    throw new InvalidDataException($"Ukuran file restore tidak sesuai: {entry.StagedPath}");
                }

                await using var stagedStream = new FileStream(entry.StagedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                var checksum = await ComputeChecksumAsync(stagedStream, cancellationToken);
                if (!checksum.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Checksum file restore tidak sesuai: {entry.StagedPath}");
                }

                if (File.Exists(entry.DestinationPath))
                {
                    var rollbackPath = BuildRollbackPath(rollbackRoot, entry.EntryPath);
                    if (IsDatabaseDestination(entry.DestinationPath))
                    {
                        await BackupDatabaseToFileAsync(entry.DestinationPath, rollbackPath, cancellationToken);
                    }
                    else
                    {
                        await CopyFileAsync(entry.DestinationPath, rollbackPath, true, cancellationToken);
                    }
                }
            }

            if (hasDatabaseEntry)
            {
                await PrepareDatabaseForRestoreAsync(cancellationToken);
            }

            foreach (var entry in marker.Entries)
            {
                await CopyFileAsync(entry.StagedPath, entry.DestinationPath, true, cancellationToken);
            }

            if (hasDatabaseEntry)
            {
                RemoveDatabaseSidecars(_paths.DatabasePath);
            }

            Directory.Delete(rollbackRoot!, true);
            Directory.Delete(stagingRoot, true);
            File.Delete(_paths.PendingRestoreMarkerPath);
            ClearRestoreError();
            _logger.Info("Restore berhasil diselesaikan saat startup.");
            return true;
        }
        catch (Exception exception)
        {
            if (hasDatabaseEntry)
            {
                await PrepareDatabaseForRestoreAsync(cancellationToken);
            }

            if (rollbackRoot is not null)
            {
                foreach (var entry in marker.Entries.AsEnumerable().Reverse())
                {
                    var rollbackPath = BuildRollbackPath(rollbackRoot, entry.EntryPath);
                    if (File.Exists(rollbackPath))
                    {
                        await CopyFileAsync(rollbackPath, entry.DestinationPath, true, cancellationToken);
                    }
                    else if (entry.HadExistingDestination == false && File.Exists(entry.DestinationPath))
                    {
                        File.Delete(entry.DestinationPath);
                    }
                }
            }

            if (hasDatabaseEntry)
            {
                RemoveDatabaseSidecars(_paths.DatabasePath);
            }

            if (rollbackRoot is not null && Directory.Exists(rollbackRoot))
            {
                Directory.Delete(rollbackRoot, true);
            }

            if (PathSafety.IsWithin(_paths.PendingRestoreDirectory, stagingRoot, true) && Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, true);
            }

            File.Delete(_paths.PendingRestoreMarkerPath);
            WriteRestoreError(exception.Message);
            throw;
        }
    }

    public async Task<bool> HasPendingRestoreAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return File.Exists(_paths.PendingRestoreMarkerPath);
    }

    public async Task<string?> GetLastRestoreErrorAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.RestoreErrorPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(_paths.RestoreErrorPath, cancellationToken);
    }

    private static void Report(
        IProgress<BackupProgressInfo>? progress,
        string stage,
        string message,
        int percent) =>
        progress?.Report(new BackupProgressInfo(stage, message, Math.Clamp(percent, 0, 100)));

    private async Task<OperationLock> AcquireOperationLockAsync(CancellationToken cancellationToken)
    {
        await OperationGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_paths.PendingRestoreDirectory);
            var lockPath = Path.Combine(_paths.PendingRestoreDirectory, "backup.lock");
            var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, true);
            return new OperationLock(stream);
        }
        catch
        {
            OperationGate.Release();
            throw;
        }
    }

    private void ValidateRestoreDestinations(PendingRestoreMarker marker, WorkspacePathsInfo workspace)
    {
        foreach (var entry in marker.Entries)
        {
            var relativePath = NormalizeEntryPath(entry.EntryPath);
            var expected = ResolveDestination(relativePath, workspace);
            var actual = Path.GetFullPath(entry.DestinationPath);
            if (expected is null || !actual.Equals(Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Tujuan restore tidak valid: {entry.EntryPath}");
            }
        }
    }

    private static string BuildRollbackPath(string rollbackRoot, string entryPath) =>
        Path.Combine(rollbackRoot, NormalizeEntryPath(entryPath).Replace('/', Path.DirectorySeparatorChar));

    private bool IsDatabaseDestination(string path) =>
        Path.GetFullPath(path).Equals(Path.GetFullPath(_paths.DatabasePath), StringComparison.OrdinalIgnoreCase);

    private async Task PrepareDatabaseForRestoreAsync(CancellationToken cancellationToken)
    {
        await Task.Run(SqliteConnection.ClearAllPools, cancellationToken);
        RemoveDatabaseSidecars(_paths.DatabasePath);
    }

    private static void RemoveDatabaseSidecars(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            var path = databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private void CleanupOrphanedStaging()
    {
        if (!Directory.Exists(_paths.PendingRestoreDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(_paths.PendingRestoreDirectory, "staging-*"))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception exception)
            {
                _logger.Warning($"Gagal membersihkan folder staging lama {directory}: {exception.Message}");
            }
        }

        var temporaryMarker = _paths.PendingRestoreMarkerPath + ".tmp";
        if (File.Exists(temporaryMarker))
        {
            File.Delete(temporaryMarker);
        }
    }

    private void ClearRestoreError()
    {
        if (File.Exists(_paths.RestoreErrorPath))
        {
            File.Delete(_paths.RestoreErrorPath);
        }
    }

    private void WriteRestoreError(string message)
    {
        Directory.CreateDirectory(_paths.PendingRestoreDirectory);
        File.WriteAllText(
            _paths.RestoreErrorPath,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{message}");
    }

    private async Task CreateDatabaseSnapshotAsync(
        string destinationPath,
        CancellationToken cancellationToken) =>
        await BackupDatabaseToFileAsync(_paths.DatabasePath, destinationPath, cancellationToken);

    private static async Task BackupDatabaseToFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await Task.Run(() =>
        {
            using var source = new SqliteConnection($"Data Source={sourcePath};Pooling=False");
            source.Open();
            using var destination = new SqliteConnection($"Data Source={destinationPath};Pooling=False");
            destination.Open();
            source.BackupDatabase(destination);
        }, cancellationToken);
    }

    private static async Task WriteManifestAsync(
        ZipArchive zip,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
    }

    private static async Task<BackupEntry> AddFileToArchiveAsync(
        ZipArchive zip,
        BackupSource source,
        CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(source.EntryPath, CompressionLevel.Optimal);
        await using var output = entry.Open();
        await using var input = new FileStream(source.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxEntryBytes)
            {
                throw new InvalidDataException($"File terlalu besar untuk backup: {source.SourcePath}");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
        }

        return new BackupEntry(
            source.EntryPath,
            total,
            Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static async Task<bool> VerifyArchiveAsync(
        string archivePath,
        BackupManifest manifest,
        CancellationToken cancellationToken)
    {
        var expected = manifest.Entries.ToDictionary(
            entry => entry.Path,
            entry => entry,
            StringComparer.OrdinalIgnoreCase);

        await using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in expected.Values)
        {
            var zipEntry = zip.GetEntry(entry.Path);
            if (zipEntry is null || zipEntry.Length != entry.SizeBytes)
            {
                return false;
            }

            await using var content = zipEntry.Open();
            var checksum = await ComputeChecksumAsync(content, cancellationToken);
            if (!checksum.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<string> ComputeChecksumAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static async Task ExtractAndVerifyAsync(
        ZipArchiveEntry zipEntry,
        string destinationPath,
        BackupEntry expected,
        CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await using var input = zipEntry.Open();
        await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > expected.SizeBytes)
            {
                throw new InvalidDataException($"File backup melebihi ukuran yang tercatat: {expected.Path}");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hash.AppendData(buffer, 0, read);
        }

        var checksum = Convert.ToHexString(hash.GetHashAndReset());
        if (total != expected.SizeBytes || !checksum.Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Checksum file backup tidak sesuai: {expected.Path}");
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var output = new FileStream(
            destinationPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            true);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static async Task WriteMarkerAsync(
        PendingRestoreMarker marker,
        string markerPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(markerPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = markerPath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            await JsonSerializer.SerializeAsync(stream, marker, JsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, markerPath, true);
    }

    private static string NormalizeEntryPath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim().Trim('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0
            || parts.Any(part => part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidDataException($"Path backup tidak aman: {path}");
        }

        return string.Join('/', parts);
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string SanitizeKind(string kind)
    {
        var sanitized = kind.Trim().Replace(' ', '-');
        return string.IsNullOrWhiteSpace(sanitized) ? "Backup" : sanitized;
    }

    private static IReadOnlyList<string> EnumerateFiles(string root, params string[] excludedRoots)
    {
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !excludedRoots.Any(excluded => PathSafety.IsWithin(excluded, file, true)))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ToRelativePath(string root, string file)
    {
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidDataException($"File di luar folder sumber: {file}");
        }

        return relative;
    }

    private static string GetFolderName(string rootPath)
    {
        var trimmed = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed);
    }

    private void ValidateDestination(string destinationRoot, WorkspacePathsInfo workspace)
    {
        var roots = new[]
        {
            workspace.MasterDocumentsDirectory,
            workspace.ProjectsDirectory,
            _paths.TemplatesDirectory
        };

        if (roots.Any(root => PathSafety.IsWithin(root, destinationRoot, true)))
        {
            throw new ArgumentException(
                "Folder backup tidak boleh berada di dalam folder sumber.",
                nameof(destinationRoot));
        }

        if (roots.Any(root => PathSafety.IsWithin(destinationRoot, root, false)))
        {
            throw new ArgumentException(
                "Folder backup tidak boleh mengandung folder sumber.",
                nameof(destinationRoot));
        }

        var databaseDirectory = Path.GetFullPath(Path.GetDirectoryName(_paths.DatabasePath)!);
        if (destinationRoot.Equals(databaseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Folder backup tidak boleh sama dengan folder database.",
                nameof(destinationRoot));
        }
    }

    private static void ValidateManifest(BackupManifest manifest, HashSet<string> archiveEntries)
    {
        if (!string.Equals(manifest.FormatVersion, BackupFormats.Current, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Format backup tidak didukung: {manifest.FormatVersion}");
        }

        var entryPaths = manifest.Entries.Select(entry => entry.Path).ToArray();
        if (entryPaths.Length != entryPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new InvalidDataException("Manifest backup berisi path duplikat.");
        }

        if (entryPaths.Any(path => !archiveEntries.Contains(NormalizeEntryPath(path))))
        {
            throw new InvalidDataException("Sebagian file dalam manifest tidak ditemukan di arsip.");
        }

        if (manifest.Entries.Any(entry => entry.SizeBytes < 0 || entry.SizeBytes > MaxEntryBytes))
        {
            throw new InvalidDataException("Ukuran file dalam manifest tidak valid.");
        }

        if (manifest.Entries.Sum(entry => entry.SizeBytes) > MaxTotalBytes)
        {
            throw new InvalidDataException("Total ukuran backup melebihi batas maksimum.");
        }

        if (manifest.Entries.Any(entry => !IsSha256Hex(entry.Sha256)))
        {
            throw new InvalidDataException("Checksum manifest tidak valid.");
        }

        foreach (var entry in manifest.Entries)
        {
            NormalizeEntryPath(entry.Path);
        }
    }

    private async Task<BackupSourceSet> BuildBackupSourcesAsync(
        BackupRequest request,
        WorkspacePathsInfo workspace,
        string destinationRoot,
        CancellationToken cancellationToken)
    {
        var files = new List<BackupSource>
        {
            new(_paths.DatabasePath, DatabaseEntryName)
        };

        if (!File.Exists(_paths.DatabasePath))
        {
            throw new FileNotFoundException("Database aplikasi tidak ditemukan.", _paths.DatabasePath);
        }

        foreach (var configPath in _paths.ConfigFilePaths.Where(File.Exists))
        {
            files.Add(new BackupSource(configPath, $"config/{Path.GetFileName(configPath)}"));
        }

        foreach (var file in EnumerateFiles(_paths.TemplatesDirectory, destinationRoot, workspace.BackupsDirectory))
        {
            files.Add(new BackupSource(file, $"templates/{ToRelativePath(_paths.TemplatesDirectory, file)}"));
        }

        foreach (var file in EnumerateFiles(workspace.MasterDocumentsDirectory, destinationRoot, workspace.BackupsDirectory))
        {
            files.Add(new BackupSource(file, $"master/{ToRelativePath(workspace.MasterDocumentsDirectory, file)}"));
        }

        await using var db = new SuratGeneratorDbContext(_options);
        var projectRows = await db.Projects
            .AsNoTracking()
            .OrderBy(project => project.ShipName)
            .ThenBy(project => project.JobName)
            .ToListAsync(cancellationToken);

        var selectedRows = request.IncludeAllProjects
            ? projectRows.ToArray()
            : projectRows.Where(project => request.ProjectIds.Contains(project.Id)).ToArray();

        var projects = selectedRows
            .Select(project => new BackupProjectInfo(
                project.Id,
                $"{project.ShipName} - {project.JobName}",
                GetFolderName(project.RootPath)))
            .ToArray();

        foreach (var project in selectedRows)
        {
            if (!PathSafety.IsWithin(workspace.ProjectsDirectory, project.RootPath, false))
            {
                _logger.Warning($"Folder proyek di luar workspace dilewati: {project.RootPath}");
                continue;
            }

            foreach (var file in EnumerateFiles(project.RootPath, destinationRoot, workspace.BackupsDirectory))
            {
                var relative = ToRelativePath(workspace.ProjectsDirectory, file);
                files.Add(new BackupSource(file, $"projects/{relative}"));
            }
        }

        return new BackupSourceSet(files, projects);
    }

    private BackupPreview BuildPreview(
        string archivePath,
        BackupManifest manifest,
        HashSet<string> archiveEntries,
        WorkspacePathsInfo workspace)
    {
        var warnings = new List<string>
        {
            "Token Google Drive tidak disertakan dalam backup. Lakukan login ulang setelah restore."
        };

        var extraEntries = archiveEntries
            .Where(entry => !manifest.Entries.Any(item => NormalizeEntryPath(item.Path).Equals(entry, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (extraEntries.Length > 0)
        {
            warnings.Add($"Arsip memiliki {extraEntries.Length} file di luar manifest; file tersebut tidak akan dipulihkan.");
        }

        var conflicts = new List<string>();
        if (File.Exists(_paths.DatabasePath))
        {
            conflicts.Add("Database aktif akan ditimpa setelah restart.");
        }

        foreach (var configPath in _paths.ConfigFilePaths.Where(File.Exists))
        {
            conflicts.Add($"Pengaturan akan ditimpa setelah restart: {Path.GetFileName(configPath)}");
        }

        if (manifest.Entries.Any(entry => entry.Path.StartsWith("master/", StringComparison.OrdinalIgnoreCase))
            && Directory.Exists(workspace.MasterDocumentsDirectory)
            && Directory.EnumerateFileSystemEntries(workspace.MasterDocumentsDirectory).Any())
        {
            conflicts.Add("Master dokumen yang sudah ada akan ditimpa setelah restart.");
        }

        foreach (var project in manifest.Projects)
        {
            var folder = Path.Combine(workspace.ProjectsDirectory, project.FolderName);
            if (Directory.Exists(folder))
            {
                conflicts.Add($"Proyek sudah ada dan akan ditimpa setelah restart: {project.Name}");
            }
        }

        return new BackupPreview(archivePath, manifest, warnings, conflicts, true);
    }

    private static HashSet<string> ResolveSelectedProjectFolders(
        RestoreRequest request,
        BackupManifest manifest)
    {
        var selectedIds = request.IncludeAllProjects
            ? manifest.Projects.Select(project => project.Id).ToArray()
            : (request.ProjectIds ?? Array.Empty<Guid>()).ToArray();

        return manifest.Projects
            .Where(project => selectedIds.Contains(project.Id))
            .Select(project => project.FolderName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool ShouldRestore(
        string entryPath,
        RestoreRequest request,
        HashSet<string> selectedProjectFolders)
    {
        if (entryPath.Equals(ManifestEntryName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (entryPath.StartsWith("database/", StringComparison.OrdinalIgnoreCase))
        {
            return request.IncludeDatabase;
        }

        if (entryPath.StartsWith("config/", StringComparison.OrdinalIgnoreCase))
        {
            return request.IncludeConfiguration;
        }

        if (entryPath.StartsWith("templates/", StringComparison.OrdinalIgnoreCase))
        {
            return request.IncludeTemplates;
        }

        if (entryPath.StartsWith("master/", StringComparison.OrdinalIgnoreCase))
        {
            return request.IncludeMasterDocuments;
        }

        if (entryPath.StartsWith("projects/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = entryPath.Split('/');
            return parts.Length >= 3
                && (request.IncludeAllProjects || selectedProjectFolders.Contains(parts[1]));
        }

        return false;
    }

    private string? ResolveDestination(string relativePath, WorkspacePathsInfo workspace)
    {
        var parts = relativePath.Split('/');
        string root;
        string relativeInsideRoot;

        switch (parts[0].ToLowerInvariant())
        {
            case "database":
                return parts.Length == 2 && parts[1].Equals("surat-generator.db", StringComparison.OrdinalIgnoreCase)
                    ? _paths.DatabasePath
                    : null;
            case "config":
                return parts.Length == 2
                    ? _paths.ConfigFilePaths.FirstOrDefault(path =>
                        Path.GetFileName(path).Equals(parts[1], StringComparison.OrdinalIgnoreCase))
                    : null;
            case "templates":
                root = _paths.TemplatesDirectory;
                relativeInsideRoot = string.Join('/', parts.Skip(1));
                break;
            case "master":
                root = workspace.MasterDocumentsDirectory;
                relativeInsideRoot = string.Join('/', parts.Skip(1));
                break;
            case "projects":
                root = Path.Combine(workspace.ProjectsDirectory, parts[1]);
                relativeInsideRoot = string.Join('/', parts.Skip(2));
                break;
            default:
                return null;
        }

        var destination = Path.GetFullPath(Path.Combine(root, relativeInsideRoot.Replace('/', Path.DirectorySeparatorChar)));
        return PathSafety.IsWithin(root, destination, false) ? destination : null;
    }

    private sealed class OperationLock : IAsyncDisposable
    {
        private readonly FileStream _stream;

        public OperationLock(FileStream stream)
        {
            _stream = stream;
        }

        public async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync();
            OperationGate.Release();
        }
    }

    private sealed record BackupSource(string SourcePath, string EntryPath);

    private sealed record BackupSourceSet(
        IReadOnlyList<BackupSource> Files,
        IReadOnlyList<BackupProjectInfo> Projects);

    private sealed class PendingRestoreMarker
    {
        public string ArchivePath { get; set; } = string.Empty;

        public string StagedDirectory { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public string? PreRestoreBackupPath { get; set; }

        public List<PendingRestoreEntry> Entries { get; set; } = new();
    }

    private sealed class PendingRestoreEntry
    {
        public string EntryPath { get; set; } = string.Empty;

        public string StagedPath { get; set; } = string.Empty;

        public string DestinationPath { get; set; } = string.Empty;

        public bool? HadExistingDestination { get; set; }

        public string Sha256 { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }
}
