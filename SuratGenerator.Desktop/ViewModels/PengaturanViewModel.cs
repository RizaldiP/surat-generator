using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class PengaturanViewModel : ObservableObject
{
    private readonly ITemplateFolderService _folderService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IFilePickerService _filePickerService;
    private readonly ITemplateFolderWatcher _folderWatcher;
    private readonly IWorkspacePathService _pathService;
    private readonly IMasterDocumentStore _documentStore;
    private readonly IConfirmationService _confirmationService;
    private readonly IBackupService _backupService;
    private readonly IBackupRecordStore _backupRecordStore;
    private readonly IProjectStore _projectStore;
    private string _folderPath = string.Empty;
    private string _masterDirectory = string.Empty;
    private string _projectsDirectory = string.Empty;
    private string _backupDirectory = string.Empty;
    private string _archivePath = string.Empty;
    private bool _includeSubfolders;
    private bool _isBusy;
    private bool _backupAllProjects = true;
    private bool _restoreAllProjects = true;
    private bool _restoreIncludeDatabase = true;
    private bool _restoreIncludeConfiguration = true;
    private bool _restoreIncludeTemplates = true;
    private bool _restoreIncludeMasterDocuments = true;
    private TemplateFolderSettingsInfo _settings = new(string.Empty, false, null);
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;
    private int _progressPercent;
    private string _progressText = string.Empty;
    private IReadOnlyList<BackupProjectSelection> _backupProjects = Array.Empty<BackupProjectSelection>();
    private IReadOnlyList<BackupProjectSelection> _restoreProjects = Array.Empty<BackupProjectSelection>();
    private IReadOnlyList<string> _backupHistory = Array.Empty<string>();
    private BackupPreview? _restorePreview;
    private bool _hasPendingRestore;
    private string _restoreError = string.Empty;

    public PengaturanViewModel(
        ITemplateFolderService folderService,
        IFolderPickerService folderPickerService,
        IFilePickerService filePickerService,
        ITemplateFolderWatcher folderWatcher,
        IWorkspacePathService pathService,
        IMasterDocumentStore documentStore,
        IConfirmationService confirmationService,
        IBackupService backupService,
        IBackupRecordStore backupRecordStore,
        IProjectStore projectStore)
    {
        _folderService = folderService;
        _folderPickerService = folderPickerService;
        _filePickerService = filePickerService;
        _folderWatcher = folderWatcher;
        _pathService = pathService;
        _documentStore = documentStore;
        _confirmationService = confirmationService;
        _backupService = backupService;
        _backupRecordStore = backupRecordStore;
        _projectStore = projectStore;
        BrowseFolderCommand = new RelayCommand(BrowseFolder, () => !IsBusy);
        SaveFolderCommand = new AsyncRelayCommand(SaveFolderAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(FolderPath));
        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync, () => !IsBusy && HasFolder);
        BrowseMasterFolderCommand = new RelayCommand(BrowseMasterFolder, () => !IsBusy);
        SaveMasterFolderCommand = new AsyncRelayCommand(
            SaveMasterFolderAsync,
            () => !IsBusy && !string.IsNullOrWhiteSpace(MasterDirectory));
        BrowseProjectsFolderCommand = new RelayCommand(BrowseProjectsFolder, () => !IsBusy);
        SaveProjectsFolderCommand = new AsyncRelayCommand(
            SaveProjectsFolderAsync,
            () => !IsBusy && !string.IsNullOrWhiteSpace(ProjectsDirectory));
        BrowseBackupFolderCommand = new RelayCommand(BrowseBackupFolder, () => !IsBusy);
        SaveBackupFolderCommand = new AsyncRelayCommand(
            SaveBackupFolderAsync,
            () => !IsBusy && !string.IsNullOrWhiteSpace(BackupDirectory));
        BrowseArchiveCommand = new RelayCommand(BrowseArchive, () => !IsBusy);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        PreviewRestoreCommand = new AsyncRelayCommand(
            PreviewRestoreAsync,
            () => !IsBusy && !string.IsNullOrWhiteSpace(ArchivePath));
        StageRestoreCommand = new AsyncRelayCommand(
            StageRestoreAsync,
            () => !IsBusy && _restorePreview is not null && !HasPendingRestore);
    }

    public IRelayCommand BrowseFolderCommand { get; }

    public IAsyncRelayCommand SaveFolderCommand { get; }

    public IAsyncRelayCommand ScanFolderCommand { get; }

    public IRelayCommand BrowseMasterFolderCommand { get; }

    public IAsyncRelayCommand SaveMasterFolderCommand { get; }

    public IRelayCommand BrowseProjectsFolderCommand { get; }

    public IAsyncRelayCommand SaveProjectsFolderCommand { get; }

    public IRelayCommand BrowseBackupFolderCommand { get; }

    public IAsyncRelayCommand SaveBackupFolderCommand { get; }

    public IRelayCommand BrowseArchiveCommand { get; }

    public IAsyncRelayCommand CreateBackupCommand { get; }

    public IAsyncRelayCommand PreviewRestoreCommand { get; }

    public IAsyncRelayCommand StageRestoreCommand { get; }

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (SetProperty(ref _folderPath, value))
            {
                SaveFolderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string MasterDirectory
    {
        get => _masterDirectory;
        set
        {
            if (SetProperty(ref _masterDirectory, value))
            {
                NotifyCommands();
            }
        }
    }

    public string ProjectsDirectory
    {
        get => _projectsDirectory;
        set
        {
            if (SetProperty(ref _projectsDirectory, value))
            {
                NotifyCommands();
            }
        }
    }

    public string BackupDirectory
    {
        get => _backupDirectory;
        set
        {
            if (SetProperty(ref _backupDirectory, value))
            {
                SaveBackupFolderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ArchivePath
    {
        get => _archivePath;
        set
        {
            if (SetProperty(ref _archivePath, value))
            {
                _restorePreview = null;
                RestoreProjects = Array.Empty<BackupProjectSelection>();
                PreviewRestoreCommand.NotifyCanExecuteChanged();
                StageRestoreCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool BackupAllProjects
    {
        get => _backupAllProjects;
        set => SetProperty(ref _backupAllProjects, value);
    }

    public bool RestoreAllProjects
    {
        get => _restoreAllProjects;
        set => SetProperty(ref _restoreAllProjects, value);
    }

    public bool RestoreIncludeDatabase
    {
        get => _restoreIncludeDatabase;
        set => SetProperty(ref _restoreIncludeDatabase, value);
    }

    public bool RestoreIncludeConfiguration
    {
        get => _restoreIncludeConfiguration;
        set => SetProperty(ref _restoreIncludeConfiguration, value);
    }

    public bool RestoreIncludeTemplates
    {
        get => _restoreIncludeTemplates;
        set => SetProperty(ref _restoreIncludeTemplates, value);
    }

    public bool RestoreIncludeMasterDocuments
    {
        get => _restoreIncludeMasterDocuments;
        set => SetProperty(ref _restoreIncludeMasterDocuments, value);
    }

    public IReadOnlyList<BackupProjectSelection> BackupProjects
    {
        get => _backupProjects;
        private set => SetProperty(ref _backupProjects, value);
    }

    public IReadOnlyList<BackupProjectSelection> RestoreProjects
    {
        get => _restoreProjects;
        private set => SetProperty(ref _restoreProjects, value);
    }

    public IReadOnlyList<string> BackupHistory
    {
        get => _backupHistory;
        private set => SetProperty(ref _backupHistory, value);
    }

    public bool HasPendingRestore
    {
        get => _hasPendingRestore;
        private set => SetProperty(ref _hasPendingRestore, value);
    }

    public string RestoreError
    {
        get => _restoreError;
        private set => SetProperty(ref _restoreError, value);
    }

    public bool HasRestoreError => !string.IsNullOrWhiteSpace(RestoreError);

    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set => SetProperty(ref _includeSubfolders, value);
    }

    public bool HasFolder => _settings.HasFolder;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyCommands();
                OnPropertyChanged(nameof(HasProgress));
            }
        }
    }

    public string FolderStatusText => _settings.FolderStatusText;

    public string LastScanText => _settings.LastScanText;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsStatusSuccess
    {
        get => _isStatusSuccess;
        private set => SetProperty(ref _isStatusSuccess, value);
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public bool HasProgress => IsBusy && ProgressPercent > 0;

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _folderService.GetSettingsAsync();
            var paths = await _pathService.GetAsync();
            ApplySettings(settings);
            FolderPath = settings.FolderPath;
            MasterDirectory = paths.MasterDocumentsDirectory;
            ProjectsDirectory = paths.ProjectsDirectory;
            BackupDirectory = paths.BackupsDirectory;
            IncludeSubfolders = settings.IncludeSubfolders;
            await LoadBackupProjectsAsync();
            await LoadBackupHistoryAsync();
            HasPendingRestore = await _backupService.HasPendingRestoreAsync();
            RestoreError = await _backupService.GetLastRestoreErrorAsync() ?? string.Empty;
            OnPropertyChanged(nameof(HasRestoreError));

            if (HasPendingRestore)
            {
                SetStatus("Restore sudah disiapkan. Restart aplikasi untuk menyelesaikannya.", true);
            }
            else if (HasRestoreError)
            {
                SetStatus($"Restore sebelumnya gagal: {RestoreError}", false);
            }
            else
            {
                SetStatus(
                    settings.HasFolder
                        ? "Folder template siap digunakan."
                        : "Pilih folder yang berisi file template .docx.",
                    true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Gagal memuat pengaturan: {ex.Message}", false);
        }
    }

    private void BrowseFolder()
    {
        var selected = _folderPickerService.PickFolder(FolderPath);
        if (selected is null)
        {
            return;
        }

        FolderPath = selected;
        SetStatus("Folder dipilih. Tekan Simpan untuk mulai memindai.", true);
    }

    private async Task SaveFolderAsync()
    {
        await RunAsync(async () =>
        {
            var settings = await _folderService.SetFolderAsync(FolderPath, IncludeSubfolders);
            ApplySettings(settings);
            await _folderWatcher.ReconfigureAsync();
            await ScanInternalAsync();
        });
    }

    private void BrowseMasterFolder()
    {
        var selected = _folderPickerService.PickFolder(MasterDirectory);
        if (selected is null)
        {
            return;
        }

        MasterDirectory = selected;
        SetStatus("Folder master dipilih. Tekan Simpan untuk memindahkan file master.", true);
    }

    private async Task SaveMasterFolderAsync()
    {
        await RunAsync(async () =>
        {
            var validation = _pathService.ValidateTargetDirectory(MasterDirectory);
            if (!validation.IsValid || validation.NormalizedPath is null)
            {
                SetStatus(validation.ErrorMessage, false);
                return;
            }

            var current = await _pathService.GetAsync();
            var newRoot = validation.NormalizedPath;
            var shouldRelocate = !string.Equals(
                current.MasterDocumentsDirectory,
                newRoot,
                StringComparison.OrdinalIgnoreCase);
            if (shouldRelocate
                && !_confirmationService.ConfirmRelocateMaster(current.MasterDocumentsDirectory, newRoot))
            {
                MasterDirectory = current.MasterDocumentsDirectory;
                return;
            }

            var updated = await _pathService.SetAsync(
                newRoot,
                current.ProjectsDirectory,
                current.BackupsDirectory);
            if (shouldRelocate)
            {
                try
                {
                    await _documentStore.RelocateAsync(current.MasterDocumentsDirectory, newRoot);
                }
                catch
                {
                    await _pathService.SetAsync(
                        current.MasterDocumentsDirectory,
                        current.ProjectsDirectory,
                        current.BackupsDirectory);
                    MasterDirectory = current.MasterDocumentsDirectory;
                    throw;
                }
            }

            MasterDirectory = updated.MasterDocumentsDirectory;
            SetStatus("Lokasi master dokumen berhasil diperbarui.", true);
        });
    }

    private void BrowseProjectsFolder()
    {
        var selected = _folderPickerService.PickFolder(ProjectsDirectory);
        if (selected is null)
        {
            return;
        }

        ProjectsDirectory = selected;
        SetStatus("Folder hasil pekerjaan dipilih. Tekan Simpan Lokasi untuk menyimpan.", true);
    }

    private async Task SaveProjectsFolderAsync()
    {
        await RunAsync(async () =>
        {
            var validation = _pathService.ValidateTargetDirectory(ProjectsDirectory);
            if (!validation.IsValid || validation.NormalizedPath is null)
            {
                SetStatus(validation.ErrorMessage, false);
                return;
            }

            var current = await _pathService.GetAsync();
            var updated = await _pathService.SetAsync(
                current.MasterDocumentsDirectory,
                validation.NormalizedPath,
                current.BackupsDirectory);
            ProjectsDirectory = updated.ProjectsDirectory;
            SetStatus("Lokasi hasil pekerjaan berhasil diperbarui. Pekerjaan lama tidak dipindahkan.", true);
        });
    }

    private void BrowseBackupFolder()
    {
        var selected = _folderPickerService.PickFolder(BackupDirectory);
        if (selected is null)
        {
            return;
        }

        BackupDirectory = selected;
        SetStatus("Folder backup dipilih. Tekan Simpan Lokasi Backup untuk menyimpan.", true);
    }

    private async Task SaveBackupFolderAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            var validation = _pathService.ValidateTargetDirectory(BackupDirectory);
            if (!validation.IsValid || validation.NormalizedPath is null)
            {
                SetStatus(validation.ErrorMessage, false);
                return;
            }

            var current = await _pathService.GetAsync(cancellationToken);
            var updated = await _pathService.SetAsync(
                current.MasterDocumentsDirectory,
                current.ProjectsDirectory,
                validation.NormalizedPath,
                cancellationToken);
            BackupDirectory = updated.BackupsDirectory;
            SetStatus("Lokasi backup berhasil diperbarui.", true);
        });
    }

    private void BrowseArchive()
    {
        var selected = _filePickerService.PickBackupArchive();
        if (selected is null)
        {
            return;
        }

        ArchivePath = selected;
        SetStatus("Arsip dipilih. Periksa isi backup sebelum restore.", true);
    }

    private async Task CreateBackupAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            var selectedIds = BackupAllProjects
                ? Array.Empty<Guid>()
                : BackupProjects.Where(item => item.IsSelected).Select(item => item.Project.Id).ToArray();
            if (!BackupAllProjects && selectedIds.Length == 0)
            {
                SetStatus("Pilih minimal satu proyek atau centang Semua proyek.", false);
                return;
            }

            var paths = await _pathService.GetAsync(cancellationToken);
            var result = await _backupService.CreateAsync(
                new BackupRequest(
                    paths.BackupsDirectory,
                    BackupKinds.Manual,
                    BackupAllProjects,
                    selectedIds,
                    "Backup manual dari Pengaturan."),
                CreateBackupProgress(),
                cancellationToken);
            await LoadBackupHistoryAsync(cancellationToken);
            SetStatus($"Backup berhasil dibuat: {result.ArchivePath}", true);
        });
    }

    private async Task PreviewRestoreAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            _restorePreview = await _backupService.PreviewAsync(ArchivePath, cancellationToken);
            RestoreProjects = _restorePreview.Manifest.Projects
                .Select(project => new BackupProjectSelection(project))
                .ToArray();
            StageRestoreCommand.NotifyCanExecuteChanged();
            var summary = _restorePreview.Conflicts.Count == 0
                ? $"Arsip valid. {_restorePreview.Manifest.Entries.Count} file siap dipulihkan."
                : $"Arsip valid dengan {_restorePreview.Conflicts.Count} data yang akan ditimpa. {_restorePreview.Manifest.Entries.Count} file siap dipulihkan.";
            SetStatus(summary, true);
        });
    }

    private async Task StageRestoreAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            if (_restorePreview is null)
            {
                SetStatus("Periksa isi backup terlebih dahulu sebelum menyiapkan restore.", false);
                return;
            }

            if (HasPendingRestore)
            {
                SetStatus("Restore sudah disiapkan. Restart aplikasi terlebih dahulu.", false);
                return;
            }

            var preview = _restorePreview;
            if (!_confirmationService.ConfirmRestore(preview))
            {
                SetStatus("Restore dibatalkan.", true);
                return;
            }

            var selectedIds = RestoreAllProjects
                ? null
                : RestoreProjects.Where(item => item.IsSelected).Select(item => item.Project.Id).ToArray();
            if (!RestoreAllProjects && (selectedIds is null || selectedIds.Length == 0))
            {
                SetStatus("Pilih minimal satu proyek atau centang Semua proyek.", false);
                return;
            }

            var request = new RestoreRequest(
                ArchivePath,
                RestoreAllProjects,
                selectedIds,
                RestoreIncludeDatabase,
                RestoreIncludeConfiguration,
                RestoreIncludeTemplates,
                RestoreIncludeMasterDocuments);
            var result = await _backupService.StageRestoreAsync(request, CreateBackupProgress(), cancellationToken);
            HasPendingRestore = true;
            RestoreError = string.Empty;
            OnPropertyChanged(nameof(HasRestoreError));
            StageRestoreCommand.NotifyCanExecuteChanged();
            await LoadBackupHistoryAsync(cancellationToken);
            SetStatus($"{result.Message} Backup pra-restore: {result.PreRestoreBackupPath}", true);
        });
    }

    private async Task LoadBackupProjectsAsync()
    {
        var projects = await _projectStore.GetAllAsync();
        BackupProjects = projects
            .Select(project => new BackupProjectInfo(
                project.Id,
                $"{project.ShipName} - {project.JobName}",
                GetFolderName(project.RootPath)))
            .Select(project => new BackupProjectSelection(project))
            .ToArray();
    }

    private async Task LoadBackupHistoryAsync(CancellationToken cancellationToken = default)
    {
        var records = await _backupRecordStore.GetAllAsync(cancellationToken);
        BackupHistory = records
            .Take(10)
            .Select(record => $"{record.CreatedAt.LocalDateTime:dd/MM/yyyy HH:mm} | {record.Kind} | {record.Status} | {record.ItemCount} file | {Path.GetFileName(record.Location)}")
            .ToArray();
    }

    private Progress<BackupProgressInfo> CreateBackupProgress() => new(report =>
    {
        ProgressPercent = report.Percent;
        ProgressText = $"{report.Stage}: {report.Message}";
        OnPropertyChanged(nameof(HasProgress));
    });

    private static string GetFolderName(string rootPath)
    {
        var trimmed = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed);
    }

    private async Task ScanFolderAsync()
    {
        await RunAsync(ScanInternalAsync);
    }

    private async Task ScanInternalAsync()
    {
        var progress = new Progress<SyncProgressInfo>(report =>
        {
            ProgressPercent = report.Percent;
            ProgressText = $"{report.Stage}: {report.Message}";
            OnPropertyChanged(nameof(HasProgress));
        });
        var result = await _folderService.ScanAsync(progress);
        var settings = await _folderService.GetSettingsAsync();
        ApplySettings(settings);
        SetStatus(result.SummaryText, !result.HasIssues);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ProgressPercent = 0;
        ProgressText = string.Empty;
        OnPropertyChanged(nameof(HasProgress));
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
        }
        finally
        {
            ProgressPercent = 100;
            IsBusy = false;
            OnPropertyChanged(nameof(HasProgress));
        }
    }

    private void ApplySettings(TemplateFolderSettingsInfo settings)
    {
        _settings = settings;
        OnPropertyChanged(nameof(HasFolder));
        OnPropertyChanged(nameof(FolderStatusText));
        OnPropertyChanged(nameof(LastScanText));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        BrowseFolderCommand.NotifyCanExecuteChanged();
        SaveFolderCommand.NotifyCanExecuteChanged();
        ScanFolderCommand.NotifyCanExecuteChanged();
        BrowseMasterFolderCommand.NotifyCanExecuteChanged();
        SaveMasterFolderCommand.NotifyCanExecuteChanged();
        BrowseProjectsFolderCommand.NotifyCanExecuteChanged();
        SaveProjectsFolderCommand.NotifyCanExecuteChanged();
        BrowseBackupFolderCommand.NotifyCanExecuteChanged();
        SaveBackupFolderCommand.NotifyCanExecuteChanged();
        BrowseArchiveCommand.NotifyCanExecuteChanged();
        CreateBackupCommand.NotifyCanExecuteChanged();
        PreviewRestoreCommand.NotifyCanExecuteChanged();
        StageRestoreCommand.NotifyCanExecuteChanged();
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
