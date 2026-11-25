using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Messages;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Documents.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class BuatSuratViewModel : ObservableObject, IDisposable
{
    private const double MinZoom = 0.2;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 0.1;
    private static readonly TimeSpan PreviewDebounce = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan AutoSaveDebounce = TimeSpan.FromSeconds(2);

    private readonly ITemplateFolderService _folderService;
    private readonly ITemplateStore _templateStore;
    private readonly IWordDocumentService _wordDocumentService;
    private readonly IPdfPageRenderer _pdfPageRenderer;
    private readonly IDraftStore _draftStore;
    private readonly IGeneratedDocumentStore _generatedDocumentStore;
    private readonly ISaveFileDialogService _saveFileDialogService;
    private readonly IConfirmationService _confirmationService;
    private readonly PreviewRequestTracker _previewRequestTracker = new();
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly ObservableCollection<TemplateFieldViewModel> _fields = new();
    private readonly ObservableCollection<PreviewPageViewModel> _previewPages = new();
    private readonly List<TemplateInfo> _allTemplates = new();
    private readonly ObservableCollection<TemplateInfo> _visibleTemplates = new();
    private TemplateInfo? _currentTemplate;
    private TemplateInfo? _selectedTemplate;
    private CancellationTokenSource? _previewCancellation;
    private CancellationTokenSource? _autoSaveCancellation;
    private string? _currentPdfPath;
    private Guid? _currentDraftId;
    private string _currentDraftName = string.Empty;
    private DateTimeOffset _currentDraftCreatedAt;
    private bool _isLoading;
    private bool _isPreviewLoading;
    private bool _isActionBusy;
    private bool _isStatusSuccess;
    private bool _resetZoomOnNextPreview;
    private string _statusMessage = string.Empty;
    private string _previewError = string.Empty;
    private string _searchText = string.Empty;
    private bool _showFavoritesOnly;
    private double _zoom = 1;

    public BuatSuratViewModel(
        ITemplateFolderService folderService,
        ITemplateStore templateStore,
        IWordDocumentService wordDocumentService,
        IPdfPageRenderer pdfPageRenderer,
        IDraftStore draftStore,
        IGeneratedDocumentStore generatedDocumentStore,
        ISaveFileDialogService saveFileDialogService,
        IConfirmationService confirmationService)
    {
        _folderService = folderService;
        _templateStore = templateStore;
        _wordDocumentService = wordDocumentService;
        _pdfPageRenderer = pdfPageRenderer;
        _draftStore = draftStore;
        _generatedDocumentStore = generatedDocumentStore;
        _saveFileDialogService = saveFileDialogService;
        _confirmationService = confirmationService;

        Fields = new ReadOnlyObservableCollection<TemplateFieldViewModel>(_fields);
        PreviewPages = new ReadOnlyObservableCollection<PreviewPageViewModel>(_previewPages);
        Templates = new ReadOnlyObservableCollection<TemplateInfo>(_visibleTemplates);
        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync, () => !IsLoading && !IsActionBusy);
        SaveDraftCommand = new AsyncRelayCommand(SaveDraftAsync, () => CanUseDocument);
        ResetFormCommand = new RelayCommand(ResetForm, () => HasTemplate || HasFields || HasPreview);
        ExportWordCommand = new AsyncRelayCommand(
            () => ExportAsync(GeneratedDocumentKind.Docx),
            () => CanUseDocument);
        ExportPdfCommand = new AsyncRelayCommand(
            () => ExportAsync(GeneratedDocumentKind.Pdf),
            () => CanUseDocument);
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => CanUseDocument);
        RetryPreviewCommand = new RelayCommand(SchedulePreviewUpdate, () => HasTemplate && !IsPreviewLoading && !IsLoading);
        ZoomInCommand = new RelayCommand(ZoomIn, () => HasPreview);
        ZoomOutCommand = new RelayCommand(ZoomOut, () => HasPreview);
        FitWidthCommand = new RelayCommand<double>(FitWidth, _ => HasPreview);

        WeakReferenceMessenger.Default.Register<TemplatesChangedMessage>(
            this,
            (_, message) => _ = LoadTemplatesAsync(message.Summary));
    }

    public IAsyncRelayCommand ScanFolderCommand { get; }

    public IAsyncRelayCommand SaveDraftCommand { get; }

    public IRelayCommand ResetFormCommand { get; }

    public IAsyncRelayCommand ExportWordCommand { get; }

    public IAsyncRelayCommand ExportPdfCommand { get; }

    public IAsyncRelayCommand PrintCommand { get; }

    public IRelayCommand RetryPreviewCommand { get; }

    public IRelayCommand ZoomInCommand { get; }

    public IRelayCommand ZoomOutCommand { get; }

    public IRelayCommand<double> FitWidthCommand { get; }

    public ReadOnlyObservableCollection<TemplateFieldViewModel> Fields { get; }

    public ReadOnlyObservableCollection<PreviewPageViewModel> PreviewPages { get; }

    public ReadOnlyObservableCollection<TemplateInfo> Templates { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyTemplateFilter();
            }
        }
    }

    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set
        {
            if (SetProperty(ref _showFavoritesOnly, value))
            {
                ApplyTemplateFilter();
                if (value && _currentTemplate is not null && !_currentTemplate.IsFavorite)
                {
                    SetStatus($"Template '{TemplateName}' tetap digunakan meski bukan favorit.", true);
                }
            }
        }
    }

    public TemplateInfo? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                _ = SelectTemplateAsync(value);
            }
        }
    }

    public bool HasTemplates => _allTemplates.Count > 0;

    public bool HasVisibleTemplates => _visibleTemplates.Count > 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyActionCommands();
                NotifyPreviewCommands();
            }
        }
    }

    public bool IsActionBusy
    {
        get => _isActionBusy;
        private set
        {
            if (SetProperty(ref _isActionBusy, value))
            {
                NotifyActionCommands();
            }
        }
    }

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set
        {
            if (SetProperty(ref _isPreviewLoading, value))
            {
                NotifyPreviewCommands();
            }
        }
    }

    public bool HasFields => _fields.Count > 0;

    public bool HasPreview => _previewPages.Count > 0;

    public bool HasTemplate => _currentTemplate is not null;

    public bool CanUseDocument => HasTemplate && HasFields && !IsLoading && !IsActionBusy;

    public string TemplateName => _currentTemplate?.Name ?? string.Empty;

    public string FieldSummary => HasTemplate
        ? $"{_fields.Count} field terdeteksi"
        : string.Empty;

    public string DraftStatusText => _currentDraftId is null
        ? "Draft baru belum disimpan"
        : $"Draft: {_currentDraftName}";

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

    public string PreviewError
    {
        get => _previewError;
        private set
        {
            if (SetProperty(ref _previewError, value))
            {
                OnPropertyChanged(nameof(HasPreviewError));
            }
        }
    }

    public bool HasPreviewError => !string.IsNullOrWhiteSpace(PreviewError);

    public double Zoom
    {
        get => _zoom;
        private set
        {
            if (SetProperty(ref _zoom, value))
            {
                OnPropertyChanged(nameof(ZoomText));
                NotifyZoomCommands();
            }
        }
    }

    public string ZoomText => $"{Math.Round(Zoom * 100)}%";

    public string SearchTooltip => "Cari berdasarkan nama, nama file, atau kategori template.";

    public string FilterTooltip => "Template diambil dari folder yang diatur di Pengaturan.";

    public string UpdateTooltip => "Pindai folder untuk memperbarui daftar template.";

    public async Task LoadTemplatesAsync(TemplateFolderScanSummary? scanSummary = null)
    {
        await _loadGate.WaitAsync();
        try
        {
            await LoadTemplatesCoreAsync(scanSummary);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task LoadTemplatesCoreAsync(TemplateFolderScanSummary? scanSummary)
    {
        IsLoading = true;
        try
        {
            var templates = (await _templateStore.GetAllAsync()).ToList();
            _allTemplates.Clear();
            _allTemplates.AddRange(templates);
            ApplyTemplateFilter();

            OnPropertyChanged(nameof(HasTemplates));
            if (_selectedTemplate is null && templates.Count > 0)
            {
                _selectedTemplate = templates[0];
                OnPropertyChanged(nameof(SelectedTemplate));
                ApplyTemplate(templates[0]);
            }

            var listText = $"Menampilkan {_visibleTemplates.Count} dari {templates.Count} template dari folder.";
            var scanText = scanSummary is null ? listText : $"{scanSummary.SummaryText} {listText}";
            SetStatus(
                templates.Count > 0
                    ? scanText
                    : "Belum ada template. Atur folder template di Pengaturan lalu pindai.",
                scanSummary is null || !scanSummary.HasIssues);
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            SetStatus("Gagal memuat daftar template.", false);
        }
        catch (Exception exception)
        {
            App.Logger.Error("Gagal memuat daftar template.", exception);
            SetStatus("Gagal memuat daftar template.", false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ScanFolderAsync()
    {
        await _loadGate.WaitAsync();
        try
        {
            await ScanFolderCoreAsync();
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task ScanFolderCoreAsync()
    {
        IsLoading = true;
        try
        {
            var settings = await _folderService.GetSettingsAsync();
            if (!settings.HasFolder)
            {
                await LoadTemplatesCoreAsync(null);
                SetStatus("Folder template belum diatur. Buka Pengaturan untuk memilih folder.", false);
                return;
            }

            var scan = await _folderService.ScanAsync();
            var templates = (await _templateStore.GetAllAsync()).ToList();
            var previousId = _selectedTemplate?.Id;
            _allTemplates.Clear();
            _allTemplates.AddRange(templates);
            ApplyTemplateFilter();

            OnPropertyChanged(nameof(HasTemplates));
            var selected = previousId is null
                ? templates.FirstOrDefault()
                : templates.FirstOrDefault(template => template.Id == previousId)
                  ?? templates.FirstOrDefault();
            if (selected is not null)
            {
                _selectedTemplate = selected;
                OnPropertyChanged(nameof(SelectedTemplate));
                if (selected.Id != _currentTemplate?.Id)
                {
                    ApplyTemplate(selected);
                }
            }
            else
            {
                _selectedTemplate = null;
                OnPropertyChanged(nameof(SelectedTemplate));
            }

            SetStatus(
                $"{scan.SummaryText} Menampilkan {_visibleTemplates.Count} dari {templates.Count} template.",
                !scan.HasIssues);
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            SetStatus($"Gagal memindai folder template: {exception.Message}", false);
        }
        catch (Exception exception)
        {
            App.Logger.Error("Gagal memindai folder template.", exception);
            SetStatus("Gagal memindai folder template. Silakan coba lagi.", false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyTemplateFilter()
    {
        var results = TemplateQueryService.Query(
            _allTemplates,
            SearchText,
            "Semua",
            TemplateSort.NameAscending,
            ShowFavoritesOnly);

        _visibleTemplates.Clear();
        foreach (var template in results)
        {
            _visibleTemplates.Add(template);
        }

        OnPropertyChanged(nameof(HasVisibleTemplates));
    }

    private async Task SelectTemplateAsync(TemplateInfo? template)
    {
        if (template is null)
        {
            return;
        }

        await _loadGate.WaitAsync();
        try
        {
            if (template.Id == _currentTemplate?.Id)
            {
                return;
            }

            CancelAutoSave();
            ClearPreview();
            ResetDraftTracking(template.Name);
            ApplyTemplate(template);
            SetStatus(
                $"Template '{template.Name}' dipilih. {_fields.Count} field terdeteksi.",
                true);
            SchedulePreviewUpdate();
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public async Task LoadDraftAsync(Guid draftId)
    {
        await _loadGate.WaitAsync();
        try
        {
            await LoadDraftCoreAsync(draftId);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task LoadDraftCoreAsync(Guid draftId)
    {
        IsActionBusy = true;
        try
        {
            var draft = await _draftStore.GetAsync(draftId);
            if (draft is null)
            {
                SetStatus("Draft tidak ditemukan.", false);
                return;
            }

            var template = await _templateStore.GetAsync(draft.TemplateId);
            if (template is null)
            {
                SetStatus("Template untuk draft ini tidak ditemukan.", false);
                return;
            }

            CancelAutoSave();
            ClearPreview();
            _selectedTemplate = template;
            OnPropertyChanged(nameof(SelectedTemplate));
            ApplyTemplate(template, draft.Values);
            _currentDraftId = draft.Id;
            _currentDraftName = draft.Name;
            _currentDraftCreatedAt = draft.CreatedAt;
            OnPropertyChanged(nameof(DraftStatusText));
            SetStatus($"Draft '{draft.Name}' dibuka.", true);
            SchedulePreviewUpdate();
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            SetStatus("Gagal membuka draft. Silakan coba lagi.", false);
        }
        catch (Exception exception)
        {
            App.Logger.Error("Gagal membuka draft.", exception);
            SetStatus("Gagal membuka draft. Silakan coba lagi.", false);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    private async Task SaveDraftAsync(CancellationToken cancellationToken)
    {
        CancelAutoSave();
        IsActionBusy = true;
        try
        {
            var saved = await PersistDraftAsync(cancellationToken);
            if (saved is not null)
            {
                SetStatus($"Draft '{saved.Name}' disimpan.", true);
            }
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            SetStatus("Gagal menyimpan draft. Silakan coba lagi.", false);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    private async Task ExportAsync(GeneratedDocumentKind kind)
    {
        var template = _currentTemplate;
        if (template is null)
        {
            return;
        }

        IsActionBusy = true;
        SetStatus(string.Empty, false);
        try
        {
            var extension = kind == GeneratedDocumentKind.Docx ? ".docx" : ".pdf";
            var suggested = $"{SafeFileName(template.Name)}_{DateTime.Now:yyyyMMdd_HHmm}{extension}";
            var outputPath = _saveFileDialogService.PickSavePath(kind, suggested);
            if (outputPath is null)
            {
                SetStatus("Ekspor dibatalkan.", false);
                return;
            }

            await _wordDocumentService.ExportAsync(
                template.StoredFilePath,
                GetValues(),
                outputPath,
                kind);

            var document = new GeneratedDocumentInfo(
                Guid.NewGuid(),
                template.Id,
                template.Name,
                Path.GetFileName(outputPath),
                kind,
                outputPath,
                new FileInfo(outputPath).Length,
                DateTimeOffset.UtcNow);
            await _generatedDocumentStore.RecordAsync(document);
            SetSuccess($"Ekspor {document.FormatLabel} berhasil: {document.DocumentName}");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            SetStatus("Gagal mengekspor dokumen. Silakan coba lagi.", false);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    private async Task PrintAsync(CancellationToken cancellationToken)
    {
        var template = _currentTemplate;
        if (template is null)
        {
            return;
        }

        IsActionBusy = true;
        SetStatus(string.Empty, false);
        try
        {
            await _wordDocumentService.PrintAsync(
                template.StoredFilePath,
                GetValues(),
                cancellationToken);

            var document = new GeneratedDocumentInfo(
                Guid.NewGuid(),
                template.Id,
                template.Name,
                $"{template.Name} - Cetak",
                GeneratedDocumentKind.Print,
                string.Empty,
                0,
                DateTimeOffset.UtcNow);
            await _generatedDocumentStore.RecordAsync(document);
            SetSuccess("Dokumen berhasil dikirim ke printer.");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            SetStatus("Gagal mencetak dokumen. Silakan coba lagi.", false);
        }
        finally
        {
            IsActionBusy = false;
        }
    }

    private void ResetForm()
    {
        if (!_confirmationService.ConfirmReset())
        {
            return;
        }

        CancelAutoSave();
        ClearPreview();
        foreach (var field in _fields)
        {
            field.ValueChanged -= OnFieldValueChanged;
        }

        _fields.Clear();
        _currentTemplate = null;
        ResetDraftTracking(string.Empty);
        Zoom = 1;
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(TemplateName));
        OnPropertyChanged(nameof(FieldSummary));
        OnPropertyChanged(nameof(DraftStatusText));
        NotifyActionCommands();
        SetSuccess("Form berhasil direset.");
    }

    private void ApplyTemplate(TemplateInfo template, IReadOnlyDictionary<string, string>? values = null)
    {
        foreach (var field in _fields)
        {
            field.ValueChanged -= OnFieldValueChanged;
        }

        _fields.Clear();
        foreach (var field in template.Fields)
        {
            var fieldViewModel = new TemplateFieldViewModel(field);
            if (values is not null
                && values.TryGetValue(field.Key, out var value))
            {
                fieldViewModel.Value = value;
            }

            fieldViewModel.ValueChanged += OnFieldValueChanged;
            _fields.Add(fieldViewModel);
        }

        _currentTemplate = template;
        _resetZoomOnNextPreview = true;
        PreviewError = string.Empty;
        OnPropertyChanged(nameof(HasFields));
        OnPropertyChanged(nameof(HasTemplate));
        OnPropertyChanged(nameof(TemplateName));
        OnPropertyChanged(nameof(FieldSummary));
        NotifyActionCommands();
    }

    private void OnFieldValueChanged(object? sender, EventArgs e)
    {
        SchedulePreviewUpdate();
        ScheduleAutoSave();
    }

    private void SchedulePreviewUpdate()
    {
        if (_currentTemplate is null)
        {
            return;
        }

        var requestId = _previewRequestTracker.Next();
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewError = string.Empty;
        IsPreviewLoading = true;
        _ = RenderAfterDebounceAsync(requestId, cancellation);
    }

    private async Task RenderAfterDebounceAsync(long requestId, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(PreviewDebounce, cancellation.Token);
            await RenderPreviewAsync(requestId, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (!_previewRequestTracker.IsCurrent(requestId))
            {
                return;
            }

            PreviewError = "Pratinjau gagal dibuat. Periksa template lalu coba lagi.";
            IsPreviewLoading = false;
        }
        finally
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                cancellation.Dispose();
                _previewCancellation = null;
            }
        }
    }

    private async Task RenderPreviewAsync(long requestId, CancellationToken cancellationToken)
    {
        var template = _currentTemplate;
        if (template is null)
        {
            return;
        }

        var pdfPath = await _wordDocumentService.RenderAsync(
            template.StoredFilePath,
            GetValues(),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var pages = await _pdfPageRenderer.RenderAsync(pdfPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_previewRequestTracker.IsCurrent(requestId))
        {
            _wordDocumentService.Cleanup(pdfPath);
            return;
        }

        _previewPages.Clear();
        foreach (var page in pages)
        {
            _previewPages.Add(page);
        }

        var previousPdfPath = _currentPdfPath;
        _currentPdfPath = pdfPath;
        if (previousPdfPath is not null)
        {
            _wordDocumentService.Cleanup(previousPdfPath);
        }

        if (_resetZoomOnNextPreview)
        {
            Zoom = 1;
            _resetZoomOnNextPreview = false;
        }

        PreviewError = string.Empty;
        IsPreviewLoading = false;
        OnPropertyChanged(nameof(HasPreview));
        ResetFormCommand.NotifyCanExecuteChanged();
        NotifyZoomCommands();
    }

    private void ScheduleAutoSave()
    {
        if (_currentTemplate is null)
        {
            return;
        }

        var previous = _autoSaveCancellation;
        _autoSaveCancellation = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();
        var cancellation = _autoSaveCancellation;
        _ = SaveDraftAfterDebounceAsync(cancellation);
    }

    private async Task SaveDraftAfterDebounceAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(AutoSaveDebounce, cancellation.Token);
            await PersistDraftAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            if (!cancellation.IsCancellationRequested)
            {
                SetStatus("Draft otomatis gagal disimpan.", false);
            }
        }
        finally
        {
            if (ReferenceEquals(_autoSaveCancellation, cancellation))
            {
                cancellation.Dispose();
                _autoSaveCancellation = null;
            }
        }
    }

    private async Task<DraftInfo?> PersistDraftAsync(CancellationToken cancellationToken)
    {
        var template = _currentTemplate;
        if (template is null)
        {
            return null;
        }

        var draft = new DraftInfo(
            _currentDraftId ?? Guid.NewGuid(),
            template.Id,
            template.Name,
            string.IsNullOrWhiteSpace(_currentDraftName)
                ? $"{template.Name} ({DateTime.Now:dd MMM yyyy HH:mm})"
                : _currentDraftName,
            GetValues(),
            _currentDraftCreatedAt,
            _currentDraftCreatedAt);

        var saved = await _draftStore.SaveAsync(draft, cancellationToken);
        _currentDraftId = saved.Id;
        _currentDraftName = saved.Name;
        _currentDraftCreatedAt = saved.CreatedAt;
        OnPropertyChanged(nameof(DraftStatusText));
        return saved;
    }

    private void ResetDraftTracking(string templateName)
    {
        _currentDraftId = null;
        _currentDraftName = string.IsNullOrWhiteSpace(templateName)
            ? string.Empty
            : $"{templateName} ({DateTime.Now:dd MMM yyyy HH:mm})";
        _currentDraftCreatedAt = default;
        OnPropertyChanged(nameof(DraftStatusText));
    }

    private void CancelAutoSave()
    {
        var cancellation = _autoSaveCancellation;
        _autoSaveCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void ClearPreview()
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = null;
        _previewRequestTracker.Next();

        _previewPages.Clear();
        if (_currentPdfPath is not null)
        {
            _wordDocumentService.Cleanup(_currentPdfPath);
            _currentPdfPath = null;
        }

        PreviewError = string.Empty;
        IsPreviewLoading = false;
        OnPropertyChanged(nameof(HasPreview));
        NotifyPreviewCommands();
    }

    private void ZoomIn() => Zoom = Math.Min(MaxZoom, Zoom + ZoomStep);

    private void ZoomOut() => Zoom = Math.Max(MinZoom, Zoom - ZoomStep);

    private void FitWidth(double availableWidth)
    {
        if (availableWidth <= 0 || _previewPages.Count == 0)
        {
            return;
        }

        var maxWidth = _previewPages.Max(page => page.PageWidth);
        Zoom = Math.Clamp(availableWidth / maxWidth, MinZoom, MaxZoom);
    }

    private IReadOnlyDictionary<string, string> GetValues()
    {
        return _fields.ToDictionary(
            field => field.Key,
            field => field.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    internal void ReportLoadFailure(string message) => SetStatus(message, false);

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }

    private void SetSuccess(string message) => SetStatus(message, true);

    private static bool IsExpectedFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or DbUpdateException
            or FileFormatException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or COMException;
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Surat" : cleaned;
    }

    private void NotifyActionCommands()
    {
        ScanFolderCommand.NotifyCanExecuteChanged();
        SaveDraftCommand.NotifyCanExecuteChanged();
        ResetFormCommand.NotifyCanExecuteChanged();
        ExportWordCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    private void NotifyPreviewCommands()
    {
        RetryPreviewCommand.NotifyCanExecuteChanged();
        NotifyZoomCommands();
    }

    private void NotifyZoomCommands()
    {
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        FitWidthCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        CancelAutoSave();
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        foreach (var field in _fields)
        {
            field.ValueChanged -= OnFieldValueChanged;
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
