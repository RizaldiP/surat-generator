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
        _navigation = navigation;
        _logger = logger;
        Fields = new ReadOnlyObservableCollection<DocumentFieldEditViewModel>(_fields);
        PreviewPages = new ReadOnlyObservableCollection<PreviewPageViewModel>(_previewPages);
        BackCommand = new RelayCommand(Back);
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanOperate);
        RefreshFromProfileCommand = new AsyncRelayCommand(RefreshFromProfileAsync, CanOperate);
        PromoteToProfileCommand = new AsyncRelayCommand<DocumentFieldEditViewModel>(
            PromoteToProfileAsync,
            field => CanOperate() && field is not null);
        RetryPreviewCommand = new RelayCommand(SchedulePreview);
        ZoomInCommand = new RelayCommand(() => Zoom = Math.Min(2, Zoom + 0.1));
        ZoomOutCommand = new RelayCommand(() => Zoom = Math.Max(0.4, Zoom - 0.1));
        FitWidthCommand = new RelayCommand<double>(FitWidth);
    }

    public ReadOnlyObservableCollection<DocumentFieldEditViewModel> Fields { get; }

    public ReadOnlyObservableCollection<PreviewPageViewModel> PreviewPages { get; }

    public IRelayCommand BackCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand RefreshFromProfileCommand { get; }

    public IAsyncRelayCommand<DocumentFieldEditViewModel> PromoteToProfileCommand { get; }

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
        var values = _aliasService.BuildValues(_fields.Select(field => field.ToModel()).ToArray());
        var pdfPath = await _wordService.RenderAsync(_baseTemplatePath!, values, cancellationToken);
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

    private async Task SaveAsync()
    {
        if (_outputPath is null || File.Exists(_outputPath) && !_confirmations.ConfirmReplaceOutput(_outputPath))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _fieldStore.PrepareOutputAsync(_documentId, _outputPath!);
            var fields = _fields.Select(field => field.ToModel()).ToArray();
            var values = _aliasService.BuildValues(fields);
            await _wordService.ExportAsync(
                _baseTemplatePath!,
                values,
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

    private async Task RefreshFromProfileAsync()
    {
        if (!_confirmations.ConfirmRefreshProfileFields())
        {
            return;
        }

        await RunAsync(async () =>
        {
            var edit = await _fieldStore.EnsureEditSessionAsync(_documentId);
            var detail = await _projectStore.GetDetailAsync(_projectId);
            var mappings = await _fieldStore.GetMappingsAsync();
            var refreshed = _aliasService.BuildFields(
                _templateFields,
                detail?.Fields ?? Array.Empty<ProjectFieldInfo>(),
                edit.Values,
                mappings);
            foreach (var field in _fields)
            {
                if (field.IsOverride)
                {
                    continue;
                }

                var candidate = refreshed.FirstOrDefault(item => item.Key == field.Key);
                if (candidate is null || candidate.IsOverride)
                {
                    continue;
                }

                field.SetValue(candidate.Value);
            }

            StatusMessage = "Field belum diubah manual diperbarui dari profil pekerjaan.";
            IsStatusSuccess = true;
            SchedulePreview();
        });
    }

    private async Task PromoteToProfileAsync(DocumentFieldEditViewModel? field)
    {
        if (field is null)
        {
            return;
        }

        var key = field.ProfileFieldKey ?? _aliasService.NormalizeKey(field.CanonicalKey);
        if (!_confirmations.ConfirmPromoteField(key))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _fieldStore.PromoteToProjectFieldsAsync(
                _documentId,
                new[] { new ProjectFieldSaveInfo(key, field.Value) });
            StatusMessage = $"Nilai field '{key}' disimpan ke profil pekerjaan.";
            IsStatusSuccess = true;
        });
    }

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
        SaveCommand.NotifyCanExecuteChanged();
        RefreshFromProfileCommand.NotifyCanExecuteChanged();
        PromoteToProfileCommand.NotifyCanExecuteChanged();
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
}
