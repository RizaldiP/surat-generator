using System.Net;
using System.Security.Cryptography;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Documents.Services;
using SuratGenerator.Infrastructure.Data;
using GoogleFile = Google.Apis.Drive.v3.Data.File;

namespace SuratGenerator.Infrastructure.Services;

public sealed class GoogleDriveService : IGoogleDriveService, IDisposable
{
    private const string UserId = "surat-generator-user";
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private readonly DriveSettingsStore _settingsStore = new();
    private readonly ITemplateStore _templateStore;
    private readonly ITemplateReader _templateReader;
    private readonly ITemplateSyncDecisionService _decisionService;
    private readonly SuratGeneratorDbContext _dbContext;
    private readonly INetworkStatusProvider _networkStatusProvider;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _credentialGate = new(1, 1);
    private UserCredential? _credential;

    public GoogleDriveService(
        ITemplateStore templateStore,
        ITemplateReader templateReader,
        ITemplateSyncDecisionService decisionService,
        INetworkStatusProvider? networkStatusProvider = null,
        IAppLogger? logger = null)
        : this(
            templateStore,
            templateReader,
            decisionService,
            DatabaseOptionsFactory.CreateProduction(),
            networkStatusProvider,
            logger)
    {
    }

    public GoogleDriveService(
        ITemplateStore templateStore,
        ITemplateReader templateReader,
        ITemplateSyncDecisionService decisionService,
        DbContextOptions<SuratGeneratorDbContext> options,
        INetworkStatusProvider? networkStatusProvider = null,
        IAppLogger? logger = null)
    {
        _templateStore = templateStore;
        _templateReader = templateReader;
        _decisionService = decisionService;
        _networkStatusProvider = networkStatusProvider ?? new SystemNetworkStatusProvider();
        _logger = logger ?? NullAppLogger.Instance;
        _dbContext = new SuratGeneratorDbContext(options);
        DatabaseInitializer.Initialize(_dbContext);
    }

    public async Task<DriveSettingsInfo> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        await _settingsStore.GetAsync(cancellationToken);

    public async Task<DriveSettingsInfo> SetFolderUrlAsync(
        string folderUrl,
        CancellationToken cancellationToken = default)
    {
        if (!GoogleDriveFolderIdParser.TryExtract(folderUrl, out var folderId))
        {
            throw new InvalidOperationException("Link folder Google Drive tidak valid.");
        }

        return await _settingsStore.UpdateFolderAsync(folderUrl.Trim(), folderId, null, cancellationToken);
    }

    public async Task<DriveSettingsInfo> ValidateFolderAsync(
        string folderUrl,
        CancellationToken cancellationToken = default)
    {
        if (!GoogleDriveFolderIdParser.TryExtract(folderUrl, out var folderId))
        {
            throw new InvalidOperationException("Link folder Google Drive tidak valid.");
        }

        EnsureOnline();
        var drive = await CreateDriveServiceAsync(cancellationToken);
        GoogleFile folder;
        try
        {
            folder = await drive.Files.Get(folderId).ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error("Gagal memproses permintaan Google Drive.", ex);
            throw new InvalidOperationException(FriendlyMessage(ex), ex);
        }

        if (string.Equals(folder.MimeType, FolderMimeType, StringComparison.Ordinal) == false)
        {
            throw new InvalidOperationException("Link tersebut bukan folder Google Drive.");
        }

        if (folder.Trashed == true)
        {
            throw new InvalidOperationException("Folder Google Drive tersebut sudah berada di tempat sampah.");
        }

        return await _settingsStore.UpdateFolderAsync(
            folderUrl.Trim(),
            folderId,
            folder.Name,
            cancellationToken);
    }

    public async Task<DriveSettingsInfo> SignInAsync(CancellationToken cancellationToken = default)
    {
        EnsureOnline();
        var credential = await GetCredentialAsync(cancellationToken);
        var drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientFactory = (IHttpClientFactory)credential,
            ApplicationName = "SuratGenerator"
        });

        string? email = null;
        try
        {
            var request = drive.About.Get();
            request.Fields = "user(emailAddress,displayName)";
            var about = await request.ExecuteAsync(cancellationToken);
            email = about.User?.EmailAddress;
        }
        catch (Exception ex)
        {
            _logger.Error("Gagal memproses permintaan Google Drive.", ex);
            throw new InvalidOperationException(FriendlyMessage(ex), ex);
        }

        return await _settingsStore.SetAccountAsync(email, cancellationToken);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _credentialGate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(AppPaths.DriveTokenPath))
            {
                File.Delete(AppPaths.DriveTokenPath);
            }

            _credential = null;
        }
        finally
        {
            _credentialGate.Release();
        }

        await _settingsStore.SetAccountAsync(null, cancellationToken);
    }

    public async Task<SyncSummaryInfo> RefreshTemplatesAsync(
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await EnsureReadyAsync(cancellationToken);
        EnsureOnline();
        var drive = await CreateDriveServiceAsync(cancellationToken);

        var remoteFiles = await ListRemoteFilesAsync(drive, settings.FolderId, cancellationToken);
        var templates = (await _templateStore.GetAllAsync(cancellationToken)).ToArray();
        var states = await _dbContext.TemplateSyncStates.ToListAsync(cancellationToken);
        var localChecksums = new Dictionary<Guid, string>();
        foreach (var template in templates)
        {
            localChecksums[template.Id] = SafeChecksum(template.StoredFilePath);
        }

        var summary = new List<string>();
        var uploaded = 0;
        var downloaded = 0;
        var skipped = 0;
        var conflicts = 0;
        var errors = 0;
        var total = remoteFiles.Count + templates.Length;
        var done = 0;

        var statesByRemote = states
            .Where(state => state.RemoteFileId is not null)
            .GroupBy(state => state.RemoteFileId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First());
        var statesByTemplate = states
            .GroupBy(state => state.TemplateId)
            .ToDictionary(group => group.Key, group => group.First());
        var remoteById = remoteFiles.ToDictionary(file => file.Id, StringComparer.Ordinal);
        var templatesById = templates.ToDictionary(template => template.Id);

        foreach (var file in remoteFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            done++;
            Report(progress, "Memeriksa template Drive", done, total, file.Name);

            if (statesByRemote.TryGetValue(file.Id, out var state))
            {
                if (state.IsLocallyDeleted)
                {
                    skipped++;
                    continue;
                }

                if (!templatesById.TryGetValue(state.TemplateId, out var template))
                {
                    try
                    {
                        await DownloadAsync(drive, file, state, state.TemplateId, progress, cancellationToken);
                        downloaded++;
                        summary.Add($"'{file.Name}' dipulihkan dari Google Drive.");
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        var message = $"Gagal mengunduh '{file.Name}': {FriendlyMessage(ex)}";
                        summary.Add(message);
                        _logger.Error(message, ex);
                    }

                    continue;
                }

                var snapshot = new TemplateSyncSnapshot(
                    template.Id,
                    template.Name,
                    template.Version,
                    localChecksums[template.Id],
                    File.GetLastWriteTimeUtc(template.StoredFilePath),
                    file.Id,
                    file.Name,
                    file.Version,
                    file.Checksum,
                    file.ModifiedAt,
                    state.LocalChecksum,
                    state.LastSyncedVersion,
                    state.IsLocallyDeleted,
                    File.Exists(template.StoredFilePath));

                var decision = _decisionService.Decide(snapshot);
                if (decision == TemplateSyncDecision.Conflict)
                {
                    conflicts++;
                    summary.Add($"Konflik '{file.Name}': salinan lokal dibuat, file asli tidak diubah.");
                    await SaveConflictCopyAsync(drive, file, state, states, progress, cancellationToken);
                    continue;
                }

                if (decision == TemplateSyncDecision.Download)
                {
                    try
                    {
                        await DownloadAsync(drive, file, state, template.Id, progress, cancellationToken);
                        downloaded++;
                        summary.Add($"'{file.Name}' diperbarui dari Google Drive.");
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        var message = $"Gagal mengunduh '{file.Name}': {FriendlyMessage(ex)}";
                        summary.Add(message);
                        _logger.Error(message, ex);
                    }
                }
                else if (decision == TemplateSyncDecision.Upload)
                {
                    try
                    {
                        await UploadAsync(drive, settings.FolderId, template, state, progress, cancellationToken);
                        uploaded++;
                        summary.Add($"'{template.Name}' diunggah ke Google Drive.");
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        var message = $"Gagal mengunggah '{template.Name}': {FriendlyMessage(ex)}";
                        summary.Add(message);
                        _logger.Error(message, ex);
                    }
                }
                else
                {
                    skipped++;
                }

                continue;
            }

            var matched = templates.FirstOrDefault(template =>
                Same(localChecksums[template.Id], file.Checksum) &&
                File.Exists(template.StoredFilePath));
            if (matched is not null)
            {
                state = GetOrCreateState(matched.Id, states);
                ApplyRemote(state, file, localChecksums[matched.Id], "Sinkron");
                skipped++;
                continue;
            }

            try
            {
                var newState = new TemplateSyncStateEntity { TemplateId = Guid.NewGuid(), Status = "Baru" };
                states.Add(newState);
                statesByTemplate[newState.TemplateId] = newState;
                await DownloadAsync(drive, file, newState, newState.TemplateId, progress, cancellationToken);
                downloaded++;
                summary.Add($"'{file.Name}' diunduh sebagai template baru.");
            }
            catch (Exception ex)
            {
                errors++;
                var message = $"Gagal mengunduh '{file.Name}': {FriendlyMessage(ex)}";
                summary.Add(message);
                _logger.Error(message, ex);
            }
        }

        foreach (var template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            done++;
            Report(progress, "Memeriksa template lokal", done, total, template.Name);

            if (statesByTemplate.TryGetValue(template.Id, out var state))
            {
                if (state.IsLocallyDeleted || state.RemoteFileId is null)
                {
                    continue;
                }

                if (!remoteById.ContainsKey(state.RemoteFileId))
                {
                    state.Status = "File remote tidak ditemukan";
                    skipped++;
                    continue;
                }

                continue;
            }

            try
            {
                var newState = new TemplateSyncStateEntity { TemplateId = template.Id, Status = "Baru" };
                states.Add(newState);
                statesByTemplate[template.Id] = newState;
                await UploadAsync(drive, settings.FolderId, template, newState, progress, cancellationToken);
                uploaded++;
                summary.Add($"'{template.Name}' diunggah sebagai template baru.");
            }
            catch (Exception ex)
            {
                errors++;
                var message = $"Gagal mengunggah '{template.Name}': {FriendlyMessage(ex)}";
                summary.Add(message);
                _logger.Error(message, ex);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var lastSync = await _settingsStore.SetLastSyncAsync(DateTimeOffset.UtcNow, cancellationToken);
        summary.Add($"Sinkronisasi selesai. {lastSync.LastSyncText}");

        return new SyncSummaryInfo(uploaded, downloaded, skipped, conflicts, errors, summary.ToArray());
    }

    public async Task<SyncSummaryInfo> UploadTemplateAsync(
        Guid templateId,
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await EnsureReadyAsync(cancellationToken);
        EnsureOnline();
        var template = await _templateStore.GetAsync(templateId, cancellationToken);
        if (template is null)
        {
            throw new InvalidOperationException("Template lokal tidak ditemukan.");
        }

        var drive = await CreateDriveServiceAsync(cancellationToken);
        var state = await _dbContext.TemplateSyncStates
            .FirstOrDefaultAsync(item => item.TemplateId == templateId, cancellationToken);
        state ??= new TemplateSyncStateEntity { TemplateId = templateId };
        if (state.IsLocallyDeleted)
        {
            state.IsLocallyDeleted = false;
        }

        _dbContext.TemplateSyncStates.Add(state);
        await UploadAsync(drive, settings.FolderId, template, state, progress, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SyncSummaryInfo(1, 0, 0, 0, 0, new[] { $"'{template.Name}' berhasil diunggah." });
    }

    public async Task<SyncSummaryInfo> UploadAllTemplatesAsync(
        IProgress<SyncProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await EnsureReadyAsync(cancellationToken);
        EnsureOnline();
        var templates = await _templateStore.GetAllAsync(cancellationToken);
        var states = await _dbContext.TemplateSyncStates.ToListAsync(cancellationToken);
        var drive = await CreateDriveServiceAsync(cancellationToken);

        var uploaded = 0;
        var skipped = 0;
        var errors = 0;
        var messages = new List<string>();

        for (var index = 0; index < templates.Count; index++)
        {
            var template = templates[index];
            var state = states.FirstOrDefault(item => item.TemplateId == template.Id);
            if (state is not null && state.IsLocallyDeleted)
            {
                skipped++;
                continue;
            }

            state ??= new TemplateSyncStateEntity { TemplateId = template.Id };
            if (!states.Contains(state))
            {
                states.Add(state);
                _dbContext.TemplateSyncStates.Add(state);
            }

            try
            {
                await UploadAsync(drive, settings.FolderId, template, state, progress, cancellationToken);
                uploaded++;
                messages.Add($"'{template.Name}' diunggah.");
            }
            catch (Exception ex)
            {
                errors++;
                var message = $"Gagal mengunggah '{template.Name}': {FriendlyMessage(ex)}";
                messages.Add(message);
                _logger.Error(message, ex);
            }

            Report(progress, "Unggah semua template", index + 1, templates.Count, template.Name);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new SyncSummaryInfo(uploaded, 0, skipped, 0, errors, messages.ToArray());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _credentialGate.Dispose();
    }

    private async Task<DriveSettingsInfo> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);
        if (!settings.HasFolder)
        {
            throw new InvalidOperationException("Link folder Google Drive belum diatur.");
        }

        if (!settings.IsSignedIn)
        {
            throw new InvalidOperationException("Silakan masuk ke akun Google terlebih dahulu.");
        }

        return settings;
    }

    private async Task<UserCredential> GetCredentialAsync(CancellationToken cancellationToken)
    {
        await _credentialGate.WaitAsync(cancellationToken);
        try
        {
            if (_credential is not null)
            {
                return _credential;
            }

            var secrets = GoogleOAuthClientLoader.Load();
            var flow = new PkceGoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = new[] { DriveService.Scope.DriveFile },
                DataStore = new DpapiDataStore()
            });
            var receiver = new LocalServerCodeReceiver();
            var installedApp = new AuthorizationCodeInstalledApp(flow, receiver);
            _credential = await installedApp.AuthorizeAsync(UserId, cancellationToken);
            return _credential;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.Error("Gagal menyiapkan kredensial Google Drive.", ex);
            throw new InvalidOperationException(FriendlyMessage(ex), ex);
        }
        finally
        {
            _credentialGate.Release();
        }
    }

    private async Task<DriveService> CreateDriveServiceAsync(CancellationToken cancellationToken)
    {
        var credential = await GetCredentialAsync(cancellationToken);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientFactory = (IHttpClientFactory)credential,
            ApplicationName = "SuratGenerator"
        });
    }

    private static async Task<List<RemoteTemplateFile>> ListRemoteFilesAsync(
        DriveService drive,
        string folderId,
        CancellationToken cancellationToken)
    {
        var files = new List<RemoteTemplateFile>();
        var pageToken = string.Empty;
        do
        {
            var request = drive.Files.List();
            request.Q = $"'{Escape(folderId)}' in parents and trashed = false and mimeType = '{DocxMimeType}'";
            request.Fields = "nextPageToken,files(id,name,modifiedTime,md5Checksum,size,appProperties)";
            request.PageSize = 1000;
            request.PageToken = pageToken;
            var response = await request.ExecuteAsync(cancellationToken);
            foreach (var file in response.Files ?? new List<GoogleFile>())
            {
                files.Add(new RemoteTemplateFile(
                    file.Id ?? string.Empty,
                    file.Name ?? "template.docx",
                    ReadProperty(file.AppProperties, "version", "1.0"),
                    file.Md5Checksum,
                    file.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow));
            }

            pageToken = response.NextPageToken ?? string.Empty;
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return files;
    }

    private async Task DownloadAsync(
        DriveService drive,
        RemoteTemplateFile file,
        TemplateSyncStateEntity state,
        Guid templateId,
        IProgress<SyncProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"suratgenerator-drive-{Guid.NewGuid():N}.docx");
        try
        {
            progress?.Report(new SyncProgressInfo("Mengunduh", 0, $"Mengunduh {file.Name}"));
            using (var stream = File.Create(tempPath))
            {
                await drive.Files.Get(file.Id).DownloadAsync(stream, cancellationToken);
            }

            var result = await _templateReader.ReadAsync(tempPath, cancellationToken);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.Error ?? "Template tidak dapat dibaca.");
            }

            var existing = templateId == Guid.Empty
                ? null
                : await _templateStore.GetAsync(templateId, cancellationToken);
            var category = existing?.Category ?? "Umum";
            var template = existing is null
                ? await _templateStore.SaveAsync(tempPath, result.Fields, cancellationToken)
                : await _templateStore.ReplaceFileAsync(
                    existing.Id,
                    tempPath,
                    result.Fields,
                    file.Name,
                    cancellationToken);
            await _templateStore.UpdateMetadataAsync(template.Id, file.Name, category, file.Version, cancellationToken);
            state.TemplateId = template.Id;
            state.RemoteFileId = file.Id;
            state.RemoteName = file.Name;
            state.RemoteVersion = file.Version;
            state.RemoteChecksum = file.Checksum;
            state.LocalChecksum = ComputeChecksum(template.StoredFilePath);
            state.RemoteModifiedAt = file.ModifiedAt;
            state.LocalModifiedAt = File.GetLastWriteTimeUtc(template.StoredFilePath);
            state.LastSyncedAt = DateTimeOffset.UtcNow;
            state.LastSyncedVersion = file.Version;
            state.Status = "Sinkron";
            state.ErrorMessage = null;
            state.IsLocallyDeleted = false;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private async Task SaveConflictCopyAsync(
        DriveService drive,
        RemoteTemplateFile file,
        TemplateSyncStateEntity state,
        List<TemplateSyncStateEntity> states,
        IProgress<SyncProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var copyState = new TemplateSyncStateEntity { TemplateId = Guid.NewGuid(), Status = "Salinan konflik" };
            await DownloadAsync(drive, file, copyState, copyState.TemplateId, progress, cancellationToken);
            copyState.RemoteFileId = null;
            copyState.Status = "Salinan konflik";
            states.Add(copyState);
            if (!_dbContext.TemplateSyncStates.Local.Any(item => item.TemplateId == copyState.TemplateId))
            {
                _dbContext.TemplateSyncStates.Add(copyState);
            }

            var copy = await _templateStore.GetAsync(copyState.TemplateId, cancellationToken);
            if (copy is not null)
            {
                await _templateStore.UpdateMetadataAsync(
                    copy.Id,
                    $"{file.Name} (konflik)",
                    copy.Category,
                    file.Version,
                    cancellationToken);
            }

            state.Status = "Konflik";
            state.ErrorMessage = "Salinan lokal dibuat; file asli tidak diubah.";
        }
        catch (Exception ex)
        {
            state.Status = "Konflik";
            state.ErrorMessage = FriendlyMessage(ex);
            _logger.Error("Gagal membuat salinan konflik Google Drive.", ex);
        }
    }

    private async Task UploadAsync(
        DriveService drive,
        string folderId,
        TemplateInfo template,
        TemplateSyncStateEntity state,
        IProgress<SyncProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(template.StoredFilePath))
        {
            throw new FileNotFoundException("File template lokal tidak ditemukan.", template.StoredFilePath);
        }

        var checksum = ComputeChecksum(template.StoredFilePath);
        var metadata = new GoogleFile
        {
            Name = string.IsNullOrWhiteSpace(template.SourceFileName)
                ? $"{template.Name}.docx"
                : template.SourceFileName,
            MimeType = DocxMimeType,
            AppProperties = new Dictionary<string, string>
            {
                ["templateKey"] = template.Id.ToString("N"),
                ["version"] = template.Version,
                ["checksum"] = checksum
            }
        };

        GoogleFile result;
        using (var stream = File.OpenRead(template.StoredFilePath))
        {
            progress?.Report(new SyncProgressInfo("Mengunggah", 0, $"Mengunggah {template.Name}"));
            if (string.IsNullOrWhiteSpace(state.RemoteFileId))
            {
                metadata.Parents = new List<string> { folderId };
                var request = drive.Files.Create(metadata, stream, DocxMimeType);
                request.Fields = "id,name,modifiedTime,md5Checksum,appProperties";
                request.ProgressChanged += upload =>
                    progress?.Report(new SyncProgressInfo(
                        "Mengunggah",
                        ToPercent(upload.BytesSent, template.FileSizeBytes),
                        $"Mengunggah {template.Name}"));
                var status = await request.UploadAsync(cancellationToken);
                if (status.Status != UploadStatus.Completed)
                {
                    throw status.Exception ?? new InvalidOperationException("Unggah gagal.");
                }

                result = request.ResponseBody ?? throw new InvalidOperationException("Respons Drive kosong.");
            }
            else
            {
                var request = drive.Files.Update(metadata, state.RemoteFileId, stream, DocxMimeType);
                request.Fields = "id,name,modifiedTime,md5Checksum,appProperties";
                request.ProgressChanged += upload =>
                    progress?.Report(new SyncProgressInfo(
                        "Mengunggah",
                        ToPercent(upload.BytesSent, template.FileSizeBytes),
                        $"Mengunggah {template.Name}"));
                var status = await request.UploadAsync(cancellationToken);
                if (status.Status != UploadStatus.Completed)
                {
                    throw status.Exception ?? new InvalidOperationException("Unggah gagal.");
                }

                result = request.ResponseBody ?? throw new InvalidOperationException("Respons Drive kosong.");
            }
        }

        state.RemoteFileId = result.Id ?? state.RemoteFileId;
        state.RemoteName = result.Name ?? metadata.Name ?? template.Name;
        state.RemoteVersion = template.Version;
        state.RemoteChecksum = result.Md5Checksum ?? checksum;
        state.LocalChecksum = checksum;
        state.RemoteModifiedAt = result.ModifiedTimeDateTimeOffset ?? DateTimeOffset.UtcNow;
        state.LocalModifiedAt = File.GetLastWriteTimeUtc(template.StoredFilePath);
        state.LastSyncedAt = DateTimeOffset.UtcNow;
        state.LastSyncedVersion = template.Version;
        state.Status = "Sinkron";
        state.ErrorMessage = null;
        state.IsLocallyDeleted = false;
    }

    private TemplateSyncStateEntity GetOrCreateState(
        Guid templateId,
        List<TemplateSyncStateEntity> states)
    {
        var state = states.FirstOrDefault(item => item.TemplateId == templateId);
        if (state is not null)
        {
            return state;
        }

        state = new TemplateSyncStateEntity { TemplateId = templateId };
        states.Add(state);
        _dbContext.TemplateSyncStates.Add(state);
        return state;
    }

    private static void ApplyRemote(
        TemplateSyncStateEntity state,
        RemoteTemplateFile file,
        string localChecksum,
        string status)
    {
        state.RemoteFileId = file.Id;
        state.RemoteName = file.Name;
        state.RemoteVersion = file.Version;
        state.RemoteChecksum = file.Checksum;
        state.LocalChecksum = localChecksum;
        state.RemoteModifiedAt = file.ModifiedAt;
        state.LocalModifiedAt = DateTimeOffset.UtcNow;
        state.LastSyncedAt = DateTimeOffset.UtcNow;
        state.LastSyncedVersion = file.Version;
        state.Status = status;
        state.ErrorMessage = null;
        state.IsLocallyDeleted = false;
    }

    private static void Report(
        IProgress<SyncProgressInfo>? progress,
        string stage,
        int done,
        int total,
        string message)
    {
        progress?.Report(new SyncProgressInfo(stage, ToPercent(done, total), message));
    }

    private static int ToPercent(long value, long total) =>
        total <= 0 ? 100 : (int)Math.Clamp(value * 100 / total, 0, 100);

    private static string ReadProperty(IDictionary<string, string>? properties, string key, string fallback) =>
        properties is not null && properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static string Escape(string value) => value.Replace("'", "\\'");

    private static string ComputeChecksum(string path)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(md5.ComputeHash(stream));
    }

    private static string SafeChecksum(string path) =>
        File.Exists(path) ? ComputeChecksum(path) : string.Empty;

    private static bool Same(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void EnsureOnline()
    {
        if (!_networkStatusProvider.IsOnline)
        {
            throw new InvalidOperationException("Tidak ada koneksi internet. Template lokal tetap dapat digunakan.");
        }
    }

    private static string FriendlyMessage(Exception exception) => exception switch
    {
        GoogleApiException google when google.HttpStatusCode == HttpStatusCode.Unauthorized =>
            "Sesi Google berakhir. Silakan masuk lại.",
        GoogleApiException google when google.HttpStatusCode == HttpStatusCode.Forbidden =>
            "Aplikasi tidak memiliki izin mengakses folder Google Drive tersebut.",
        GoogleApiException google when google.HttpStatusCode == HttpStatusCode.NotFound =>
            "Folder atau file Google Drive tidak ditemukan.",
        HttpRequestException => "Koneksi internet tidak tersedia.",
        OperationCanceledException => "Operasi dibatalkan.",
        InvalidOperationException => exception.Message,
        _ => exception.Message
    };

    private sealed record RemoteTemplateFile(
        string Id,
        string Name,
        string Version,
        string? Checksum,
        DateTimeOffset ModifiedAt);
}
