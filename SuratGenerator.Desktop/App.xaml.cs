using System.Windows;
using System.Windows.Threading;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Documents.Services;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Desktop;

public partial class App : Application
{
    private static readonly IAppLogger AppLogger = new FileLogger();

    private static readonly Lazy<WordPdfRenderer> WordRenderer = new(() => new WordPdfRenderer(AppLogger));

    private static readonly Lazy<ITemplateFolderService> FolderService = new(() => new TemplateFolderService(
        new TemplateStore(AppLogger),
        new TemplateReader(),
        converter: WordPdfRenderer,
        logger: AppLogger));

    private static readonly Lazy<ITemplateFolderWatcher> FolderWatcher = new(() =>
        new TemplateFolderWatcher(TemplateFolder, AppLogger));

    private static readonly Lazy<IDocumentCategoryStore> DocumentCategoryStoreService = new(() =>
        new DocumentCategoryStore(AppLogger));

    private static readonly Lazy<IWorkspacePathService> WorkspacePathServiceInstance = new(() =>
        new WorkspacePathService());

    private static readonly Lazy<IWorkspaceFileService> WorkspaceFileServiceInstance = new(() =>
        new WorkspaceFileService());

    private static readonly Lazy<ICategoryChecklistItemStore> CategoryChecklistItemStoreService = new(() =>
        new CategoryChecklistItemStore(AppLogger));

    private static readonly Lazy<IMasterDocumentStore> MasterDocumentStoreService = new(() =>
        new MasterDocumentStore(
            AppLogger,
            WorkspacePathServiceInstance.Value,
            WorkspaceFileServiceInstance.Value));

    private static readonly Lazy<IProjectStore> ProjectStoreService = new(() =>
        new ProjectStore(
            AppLogger,
            WorkspacePathServiceInstance.Value,
            WorkspaceFileServiceInstance.Value,
            DocumentCategories,
            MasterDocuments,
            CategoryChecklistItems));

    private static readonly Lazy<IFilePickerService> FilePickerServiceInstance = new(() =>
        new FilePickerService());

    private static readonly Lazy<IProjectDialogService> ProjectDialogServiceInstance = new(() =>
        new ProjectDialogService());

    private static readonly Lazy<IExplorerService> ExplorerServiceInstance = new(() =>
        new ExplorerService());

    private static readonly Lazy<ITemplateReader> TemplateReaderInstance = new(() =>
        new TemplateReader());

    private static readonly Lazy<IPlaceholderAliasService> PlaceholderAliasServiceInstance = new(() =>
        new PlaceholderAliasService());

    private static readonly Lazy<IDocumentFieldValueStore> DocumentFieldValueStoreInstance = new(() =>
        new DocumentFieldValueStore(AppLogger));

    private static readonly Lazy<IBackupRecordStore> BackupRecordStoreService = new(() =>
        new BackupRecordStore(AppLogger));

    private static readonly Lazy<IBackupService> BackupServiceInstance = new(() =>
        new BackupService(
            DatabaseOptionsFactory.CreateProduction(),
            WorkspacePaths,
            AppLogger,
            () => BackupRecords));

    internal static IAppLogger Logger => AppLogger;

    internal static WordPdfRenderer WordPdfRenderer => WordRenderer.Value;

    internal static NavigationService Navigation { get; } = new();

    internal static ITemplateFolderService TemplateFolder => FolderService.Value;

    internal static ITemplateFolderWatcher TemplateFolderWatcher => FolderWatcher.Value;

    internal static IDocumentCategoryStore DocumentCategories => DocumentCategoryStoreService.Value;

    internal static IWorkspacePathService WorkspacePaths => WorkspacePathServiceInstance.Value;

    internal static ICategoryChecklistItemStore CategoryChecklistItems => CategoryChecklistItemStoreService.Value;

    internal static IMasterDocumentStore MasterDocuments => MasterDocumentStoreService.Value;

    internal static IProjectStore Projects => ProjectStoreService.Value;

    internal static IFilePickerService FilePicker => FilePickerServiceInstance.Value;

    internal static IProjectDialogService ProjectDialogs => ProjectDialogServiceInstance.Value;

    internal static IExplorerService Explorer => ExplorerServiceInstance.Value;

    internal static ITemplateReader TemplateReader => TemplateReaderInstance.Value;

    internal static IPlaceholderAliasService PlaceholderAliases => PlaceholderAliasServiceInstance.Value;

    internal static IDocumentFieldValueStore DocumentFieldValues => DocumentFieldValueStoreInstance.Value;

    internal static IBackupRecordStore BackupRecords => BackupRecordStoreService.Value;

    internal static IBackupService BackupService => BackupServiceInstance.Value;

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
        await StartBackupMaintenanceAsync();
        await StartWatcherAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (FolderWatcher.IsValueCreated)
        {
            FolderWatcher.Value.Dispose();
        }

        if (WordRenderer.IsValueCreated)
        {
            WordRenderer.Value.Dispose();
        }

        if (DocumentCategoryStoreService.IsValueCreated)
        {
            (DocumentCategoryStoreService.Value as IDisposable)?.Dispose();
        }

        if (CategoryChecklistItemStoreService.IsValueCreated)
        {
            (CategoryChecklistItemStoreService.Value as IDisposable)?.Dispose();
        }

        if (MasterDocumentStoreService.IsValueCreated)
        {
            (MasterDocumentStoreService.Value as IDisposable)?.Dispose();
        }

        if (ProjectStoreService.IsValueCreated)
        {
            (ProjectStoreService.Value as IDisposable)?.Dispose();
        }

        if (DocumentFieldValueStoreInstance.IsValueCreated)
        {
            (DocumentFieldValueStoreInstance.Value as IDisposable)?.Dispose();
        }

        if (BackupRecordStoreService.IsValueCreated)
        {
            (BackupRecordStoreService.Value as IDisposable)?.Dispose();
        }

        if (BackupServiceInstance.IsValueCreated)
        {
            (BackupServiceInstance.Value as IDisposable)?.Dispose();
        }

        Navigation.Unregister();
        if (Logger is IDisposable logger)
        {
            logger.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Exception UI tidak tertangani.", e.Exception);
        ShowFatalMessage();
        e.Handled = true;
        Shutdown(1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error("Exception aplikasi tidak tertangani.", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("Exception task tidak tertangani.", e.Exception);
        e.SetObserved();
    }

    private static async Task StartBackupMaintenanceAsync()
    {
        try
        {
            var restored = await BackupServiceInstance.Value.FinalizePendingRestoreAsync();
            if (restored)
            {
                Logger.Info("Restore berhasil diselesaikan sebelum aplikasi siap digunakan.");
            }

            await BackupServiceInstance.Value.EnsureDailyBackupAsync();
        }
        catch (Exception exception)
        {
            Logger.Error("Gagal menjalankan perawatan backup saat startup.", exception);
            ShowBackupWarning($"Gagal menjalankan perawatan backup saat startup: {exception.Message}");
        }
    }

    private static async Task StartWatcherAsync()
    {
        try
        {
            await TemplateFolderWatcher.StartAsync();
        }
        catch (Exception exception)
        {
            Logger.Error("Gagal memulai pemantau folder template.", exception);
        }
    }

    private static void ShowBackupWarning(string message)
    {
        try
        {
            MessageBox.Show(
                $"{message}\n\nDetail tersimpan di log aplikasi.",
                "Peringatan Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch
        {
        }
    }

    private static void ShowFatalMessage()
    {
        try
        {
            MessageBox.Show(
                "Terjadi kesalahan tak terduga. Aplikasi akan ditutup. Detail tersimpan di log aplikasi.",
                "Surat Generator",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
        }
    }
}
