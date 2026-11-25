using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class ProjectCreateViewModel : ObservableObject
{
    private readonly IProjectStore _projectStore;
    private readonly IWorkspacePathService _pathService;
    private readonly INavigationService _navigation;
    private readonly IProjectDialogService _dialogService;
    private readonly ObservableCollection<CustomFieldRow> _customFields = new();
    private string _shipName = string.Empty;
    private string _jobName = string.Empty;
    private string _contractNumber = string.Empty;
    private string _contractDate = string.Empty;
    private string _contractValue = string.Empty;
    private string _company = string.Empty;
    private string _recipient = string.Empty;
    private string _workLocation = string.Empty;
    private string _outputDirectory = string.Empty;
    private bool _isBusy;
    private int _progressPercent;
    private string _progressStage = string.Empty;
    private string _progressMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;

    public ProjectCreateViewModel(
        IProjectStore projectStore,
        IWorkspacePathService pathService,
        INavigationService navigation,
        IProjectDialogService dialogService)
    {
        _projectStore = projectStore;
        _pathService = pathService;
        _navigation = navigation;
        _dialogService = dialogService;
        CustomFields = new ReadOnlyObservableCollection<CustomFieldRow>(_customFields);
        AddCustomFieldCommand = new RelayCommand(AddCustomField, () => !IsBusy);
        RemoveCustomFieldCommand = new RelayCommand<CustomFieldRow>(RemoveCustomField, _ => !IsBusy);
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => !IsBusy && CanCreate);
        CancelCommand = new RelayCommand(Cancel, () => !IsBusy);
    }

    public ReadOnlyObservableCollection<CustomFieldRow> CustomFields { get; }

    public IRelayCommand AddCustomFieldCommand { get; }

    public IRelayCommand<CustomFieldRow> RemoveCustomFieldCommand { get; }

    public IAsyncRelayCommand CreateCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public string ShipName
    {
        get => _shipName;
        set
        {
            if (SetProperty(ref _shipName, value))
            {
                CreateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string JobName
    {
        get => _jobName;
        set
        {
            if (SetProperty(ref _jobName, value))
            {
                CreateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ContractNumber
    {
        get => _contractNumber;
        set => SetProperty(ref _contractNumber, value);
    }

    public string ContractDate
    {
        get => _contractDate;
        set => SetProperty(ref _contractDate, value);
    }

    public string ContractValue
    {
        get => _contractValue;
        set => SetProperty(ref _contractValue, value);
    }

    public string Company
    {
        get => _company;
        set => SetProperty(ref _company, value);
    }

    public string Recipient
    {
        get => _recipient;
        set => SetProperty(ref _recipient, value);
    }

    public string WorkLocation
    {
        get => _workLocation;
        set => SetProperty(ref _workLocation, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        private set => SetProperty(ref _outputDirectory, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CreateCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                AddCustomFieldCommand.NotifyCanExecuteChanged();
                RemoveCustomFieldCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(HasProgress));
            }
        }
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressStage
    {
        get => _progressStage;
        private set => SetProperty(ref _progressStage, value);
    }

    public string ProgressMessage
    {
        get => _progressMessage;
        private set => SetProperty(ref _progressMessage, value);
    }

    public bool HasProgress => IsBusy && ProgressPercent > 0;

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

    private bool CanCreate => !string.IsNullOrWhiteSpace(ShipName) && !string.IsNullOrWhiteSpace(JobName);

    public async Task InitializeAsync()
    {
        try
        {
            var paths = await _pathService.GetAsync();
            OutputDirectory = paths.ProjectsDirectory;
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memuat lokasi output: {exception.Message}", false);
        }
    }

    private void AddCustomField() => _customFields.Add(new CustomFieldRow());

    private void RemoveCustomField(CustomFieldRow? row)
    {
        if (row is not null)
        {
            _customFields.Remove(row);
        }
    }

    private async Task CreateAsync()
    {
        IsBusy = true;
        ProgressPercent = 0;
        ProgressStage = string.Empty;
        ProgressMessage = string.Empty;
        OnPropertyChanged(nameof(HasProgress));
        var progress = new Progress<ProjectCreationProgressInfo>(report =>
        {
            ProgressPercent = report.Percent;
            ProgressStage = report.Stage;
            ProgressMessage = report.Message;
            OnPropertyChanged(nameof(HasProgress));
        });

        try
        {
            var request = new ProjectCreationRequest(
                ShipName.Trim(),
                JobName.Trim(),
                BuildFields());
            var result = await _projectStore.CreateAsync(request, progress);
            if (result.IsDuplicate)
            {
                HandleDuplicate(result);
                return;
            }

            if (result.Project is not null)
            {
                SetStatus(result.Message, true);
                try
                {
                    _navigation.NavigateTo<Views.Pages.ProjectDetailPage>(result.Project.Id);
                }
                catch (Exception navigationException)
                {
                    Exception detail = navigationException;
                    while (detail.InnerException is not null)
                    {
                        detail = detail.InnerException;
                    }
                    SetStatus($"Pekerjaan dibuat, tetapi halaman detail gagal dibuka: {detail.Message}", false);
                }
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal membuat pekerjaan: {exception.Message}", false);
        }
        finally
        {
            ProgressPercent = 100;
            IsBusy = false;
            OnPropertyChanged(nameof(HasProgress));
        }
    }

    private void HandleDuplicate(ProjectCreationResult result)
    {
        var decision = _dialogService.ShowDuplicate(new ProjectDuplicateInfo(
            result.ExistingProjectId ?? Guid.Empty,
            ShipName.Trim(),
            JobName.Trim(),
            result.RootPath));
        switch (decision)
        {
            case DuplicateProjectDecision.OpenExisting when result.ExistingProjectId.HasValue:
                _navigation.NavigateTo<Views.Pages.ProjectDetailPage>(result.ExistingProjectId.Value);
                break;
            case DuplicateProjectDecision.UseDifferentName:
                SetStatus("Ubah nama kapal atau pekerjaan, lalu coba lagi.", false);
                break;
            default:
                SetStatus("Pembuatan pekerjaan dibatalkan.", false);
                break;
        }
    }

    private IReadOnlyList<ProjectFieldRequest> BuildFields()
    {
        var fields = new List<ProjectFieldRequest>
        {
            new(ProfileFieldKeys.ContractNumber, ContractNumber.Trim(), "Text"),
            new(ProfileFieldKeys.ContractDate, ContractDate.Trim(), "Date"),
            new(ProfileFieldKeys.ContractValue, ContractValue.Trim(), "Currency"),
            new(ProfileFieldKeys.Company, Company.Trim(), "Text"),
            new(ProfileFieldKeys.Recipient, Recipient.Trim(), "Text"),
            new(ProfileFieldKeys.WorkLocation, WorkLocation.Trim(), "Text")
        };

        foreach (var row in _customFields)
        {
            fields.Add(new ProjectFieldRequest(row.Key.Trim(), row.Value, "Text"));
        }

        return fields;
    }

    private void Cancel() => _navigation.NavigateTo<Views.Pages.ProjectListPage>();

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
