using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Documents.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class DocumentEditorViewModel : ObservableObject, IDisposable
{
    private const int PreviewDebounceMilliseconds = 350;

    private readonly IProjectStore _projectStore;
    private readonly IDocumentFieldValueStore _fieldStore;
    private readonly IPlaceholderAliasService _aliasService;
    private readonly ITemplateReader _templateReader;
    private readonly IWordDocumentService _wordService;
    private readonly IPdfPageRenderer _pdfRenderer;
    private readonly IConfirmationService _confirmations;
    private readonly ISaveFileDialogService _saveFileDialogService;
    private readonly IGeneratedDocumentStore _generatedDocumentStore;
    private readonly INavigationService _navigation;
    private readonly IAppLogger _logger;
    private readonly ObservableCollection<DocumentFieldEditViewModel> _fields = new();
    private readonly ObservableCollection<PreviewPageViewModel> _previewPages = new();
    private readonly PreviewRequestTracker _previewTracker = new();
    private Guid _documentId;
    private Guid _projectId;
    private IReadOnlyList<TemplateField> _templateFields = Array.Empty<TemplateField>();
    private string? _baseTemplatePath;
    private string? _outputPath;
    private CancellationTokenSource? _previewCancellation;
    private string? _currentPdfPath;
    private string _documentName = "Dokumen";
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;
    private string _previewError = string.Empty;
    private bool _isPreviewLoading;
    private bool _isBusy;
    private double _zoom = 1;

    public DocumentEditorViewModel(
        IProjectStore projectStore,
        IDocumentFieldValueStore fieldStore,
        IPlaceholderAliasService aliasService,
        ITemplateReader templateReader,
        IWordDocumentService wordService,
        IPdfPageRenderer pdfRenderer,
        IConfirmationService confirmations,
        ISaveFileDialogService saveFileDialogService,
        IGeneratedDocumentStore generatedDocumentStore,
        INavigationService navigation,
        IAppLogger logger)
    {
        _projectStore = projectStore;
        _fieldStore = fieldStore;
        _aliasService = aliasService;
        _templateReader = templateReader;
        _wordService = wordService;
        _pdfRenderer = pdfRenderer;
        _confirmations = confirmations;
        _saveFileDialogService = saveFileDialogService;
        _generatedDocumentStore = generatedDocumentStore;
        _navigation = navigation;
        _logger = logger;
        Fields = new ReadOnlyObservableCollection<DocumentFieldEditViewModel>(_fields);
        PreviewPages = new ReadOnlyObservableCollection<PreviewPageViewModel>(_previewPages);
        BackCommand = new RelayCommand(Back);
        SaveDraftCommand = new AsyncRelayCommand(SaveDraftAsync, CanOperate);
        ResetFormCommand = new AsyncRelayCommand(ResetAsync, CanOperate);
        ExportWordCommand = new AsyncRelayCommand(
            () => ExportAsync(GeneratedDocumentKind.Docx),
            CanOperate);
        ExportPdfCommand = new AsyncRelayCommand(
            () => ExportAsync(GeneratedDocumentKind.Pdf),
            CanOperate);
        PrintCommand = new AsyncRelayCommand(PrintAsync, CanOperate);
        RetryPreviewCommand = new RelayCommand(SchedulePreview);
        ZoomInCommand = new RelayCommand(() => Zoom = Math.Min(2, Zoom + 0.1));
        ZoomOutCommand = new RelayCommand(() => Zoom = Math.Max(0.4, Zoom - 0.1));
        FitWidthCommand = new RelayCommand<double>(FitWidth);
    }

    public ReadOnlyObservableCollection<DocumentFieldEditViewModel> Fields { get; }

    public ReadOnlyObservableCollection<PreviewPageViewModel> PreviewPages { get; }

    public IRelayCommand BackCommand { get; }

    public IAsyncRelayCommand SaveDraftCommand { get; }

    public IAsyncRelayCommand ResetFormCommand { get; }

    public IAsyncRelayCommand ExportWordCommand { get; }

    public IAsyncRelayCommand ExportPdfCommand { get; }

    public IAsyncRelayCommand PrintCommand { get; }

    public IRelayCommand RetryPreviewCommand { get; }

    public IRelayCommand ZoomInCommand { get; }

    public IRelayCommand ZoomOutCommand { get; }

    public IRelayCommand<double> FitWidthCommand { get; }

    public string DocumentName
    {
        get => _documentName;
        private set => SetProperty(ref _documentName, value);
    }

    public bool HasFields => _fields.Count > 0;

    public bool HasPreview => _previewPages.Count > 0;

    public bool HasPreviewError => !string.IsNullOrWhiteSpace(_previewError);

    public bool HasStatus => !string.IsNullOrWhiteSpace(_statusMessage);

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set => SetProperty(ref _isPreviewLoading, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyCommands();
            }
        }
    }

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

    public double Zoom
    {
        get => _zoom;
        private set
        {
            if (SetProperty(ref _zoom, value))
            {
                OnPropertyChanged(nameof(ZoomText));
            }
        }
    }

    public string ZoomText => $"{Zoom:0%}";

    public void Initialize(Guid documentId)
    {
        _documentId = documentId;
    }

    public async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var edit = await _fieldStore.EnsureEditSessionAsync(_documentId);
            _projectId = edit.ProjectId;
            _baseTemplatePath = edit.BaseTemplatePath;
            _outputPath = edit.OutputPath;
            DocumentName = edit.DisplayName;
            var read = await _templateReader.ReadAsync(edit.BaseTemplatePath);
            if (!read.IsSuccess)
            {
                throw new InvalidOperationException(read.Error ?? "Template Word tidak dapat dibaca.");
            }

            _templateFields = read.Fields;
            var detail = await _projectStore.GetDetailAsync(edit.ProjectId);
            var mappings = await _fieldStore.GetMappingsAsync();
            var fields = _aliasService.BuildFields(
                read.Fields,
                detail?.Fields ?? Array.Empty<ProjectFieldInfo>(),
                edit.Values,
                mappings);
            ApplyFields(fields);
            StatusMessage = $"Template dasar: {Path.GetFileName(edit.BaseTemplatePath)}";
            IsStatusSuccess = true;
            SchedulePreview();
        });
    }

    public void Dispose()
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = null;
        if (_currentPdfPath is not null)
        {
            _wordService.Cleanup(_currentPdfPath);
            _currentPdfPath = null;
        }
    }

    private bool CanOperate() => !IsBusy && _documentId != Guid.Empty && _baseTemplatePath is not null;

    private void OnFieldPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentFieldEditViewModel.Value))
        {
            SchedulePreview();
        }
    }

    private void SchedulePreview()
    {
        if (_baseTemplatePath is null)
        {
            return;
        }

        var requestId = _previewTracker.Next();
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewError = string.Empty;
        IsPreviewLoading = true;
        _ = RenderAfterDebounceAsync(requestId, cancellation.Token);
    }

    private async Task RenderAfterDebounceAsync(long requestId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PreviewDebounceMilliseconds, cancellationToken);
            await RenderPreviewAsync(requestId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Error("Gagal membuat pratinjau editor dokumen.", exception);
            if (_previewTracker.IsCurrent(requestId))
            {
                PreviewError = "Pratinjau gagal dibuat. Periksa template Word lalu coba lagi.";
                IsPreviewLoading = false;
            }
        }
    }

    private async Task RenderPreviewAsync(long requestId, CancellationToken cancellationToken)
    {
        var pdfPath = await _wordService.RenderAsync(_baseTemplatePath!, BuildValues(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var pages = await _pdfRenderer.RenderAsync(pdfPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_previewTracker.IsCurrent(requestId))
        {
            _wordService.Cleanup(pdfPath);
            return;
        }

        _previewPages.Clear();
        foreach (var page in pages)
        {
            _previewPages.Add(page);
        }

        var previous = _currentPdfPath;
        _currentPdfPath = pdfPath;
        if (previous is not null)
        {
            _wordService.Cleanup(previous);
        }

        PreviewError = string.Empty;
        IsPreviewLoading = false;
        OnPropertyChanged(nameof(HasPreview));
    }

    private async Task SaveDraftAsync()
    {
        if (_outputPath is null || File.Exists(_outputPath) && !_confirmations.ConfirmReplaceOutput(_outputPath))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _fieldStore.PrepareOutputAsync(_documentId, _outputPath!);
            var fields = _fields.Select(field => field.ToModel()).ToArray();
            await _wordService.ExportAsync(
                _baseTemplatePath!,
                _aliasService.BuildValues(fields),
                _outputPath!,
                GeneratedDocumentKind.Docx);
            await _fieldStore.SaveAsync(new DocumentFieldEditSaveRequest(
                _documentId,
                _projectId,
                _outputPath!,
                fields.Select(field => new DocumentFieldValueSaveInfo(
                    field.CanonicalKey,
                    field.Value,
                    field.IsOverride)).ToArray()));
            var unfilled = _aliasService.FindUnfilledPlaceholders(fields);
            var status = unfilled.Count == 0
                ? DocumentStatuses.Completed
                : DocumentStatuses.InProgress;
            await _projectStore.SetDocumentStatusAsync(_documentId, status);
            StatusMessage = unfilled.Count == 0
                ? $"Hasil edit disimpan dan ditandai Selesai: {Path.GetFileName(_outputPath!)}"
                : $"Hasil edit disimpan, tetapi {unfilled.Count} place holder belum terisi: {string.Join(", ", unfilled)}";
            IsStatusSuccess = unfilled.Count == 0;
        });
    }

    private async Task ResetAsync()
    {
        if (!_confirmations.ConfirmReset())
        {
            return;
        }

        await RunAsync(async () =>
        {
            var detail = await _projectStore.GetDetailAsync(_projectId);
            var mappings = await _fieldStore.GetMappingsAsync();
            var fields = _aliasService.BuildFields(
                _templateFields,
                detail?.Fields ?? Array.Empty<ProjectFieldInfo>(),
                Array.Empty<DocumentFieldValueInfo>(),
                mappings);
            ApplyFields(fields);
            StatusMessage = "Field dikembalikan ke nilai profil pekerjaan.";
            IsStatusSuccess = true;
            SchedulePreview();
        });
    }

    private async Task ExportAsync(GeneratedDocumentKind kind)
    {
        var extension = kind == GeneratedDocumentKind.Docx ? ".docx" : ".pdf";
        var suggested = $"{SafeFileName(DocumentName)}_{DateTime.Now:yyyyMMdd_HHmm}{extension}";
        var outputPath = _saveFileDialogService.PickSavePath(kind, suggested);
        if (outputPath is null)
        {
            StatusMessage = "Ekspor dibatalkan.";
            IsStatusSuccess = false;
            return;
        }

        await RunAsync(async () =>
        {
            await _wordService.ExportAsync(
                _baseTemplatePath!,
                BuildValues(),
                outputPath,
                kind);
            var document = new GeneratedDocumentInfo(
                Guid.NewGuid(),
                _documentId,
                Path.GetFileNameWithoutExtension(_baseTemplatePath!),
                Path.GetFileName(outputPath),
                kind,
                outputPath,
                new FileInfo(outputPath).Length,
                DateTimeOffset.UtcNow);
            await _generatedDocumentStore.RecordAsync(document);
            StatusMessage = $"Ekspor {document.FormatLabel} berhasil: {document.DocumentName}";
            IsStatusSuccess = true;
        });
    }

    private async Task PrintAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () =>
        {
            await _wordService.PrintAsync(_baseTemplatePath!, BuildValues(), cancellationToken);
            var document = new GeneratedDocumentInfo(
                Guid.NewGuid(),
                _documentId,
                Path.GetFileNameWithoutExtension(_baseTemplatePath!),
                $"{DocumentName} - Cetak",
                GeneratedDocumentKind.Print,
                string.Empty,
                0,
                DateTimeOffset.UtcNow);
            await _generatedDocumentStore.RecordAsync(document);
            StatusMessage = "Dokumen berhasil dikirim ke printer.";
            IsStatusSuccess = true;
        });
    }

    private void ApplyFields(IReadOnlyList<DocumentFieldEditField> fields)
    {
        foreach (var field in _fields)
        {
            field.PropertyChanged -= OnFieldPropertyChanged;
        }

        _fields.Clear();
        foreach (var field in fields)
        {
            var viewModel = new DocumentFieldEditViewModel(field);
            viewModel.PropertyChanged += OnFieldPropertyChanged;
            _fields.Add(viewModel);
        }

        OnPropertyChanged(nameof(HasFields));
    }

    private IReadOnlyDictionary<string, string> BuildValues() =>
        _aliasService.BuildValues(_fields.Select(field => field.ToModel()).ToArray());

    private void FitWidth(double availableWidth)
    {
        if (_previewPages.Count == 0 || availableWidth <= 24)
        {
            return;
        }

        var pageWidth = _previewPages[0].PageWidth;
        if (pageWidth <= 0)
        {
            return;
        }

        Zoom = Math.Max(0.4, Math.Min(2, (availableWidth - 24) / pageWidth));
    }

    private void Back() => _navigation.NavigateTo<Views.Pages.ProjectDetailPage>(_projectId);

    private void NotifyCommands()
    {
        SaveDraftCommand.NotifyCanExecuteChanged();
        ResetFormCommand.NotifyCanExecuteChanged();
        ExportWordCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        PrintCommand.NotifyCanExecuteChanged();
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _logger.Error("Gagal mengerjakan editor dokumen.", exception);
            StatusMessage = exception.Message;
            IsStatusSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Surat" : cleaned;
    }
}
