using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Desktop.Services;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class ProjectListViewModel : ObservableObject
{
    private readonly IProjectStore _projectStore;
    private readonly INavigationService _navigation;
    private readonly ObservableCollection<ProjectInfo> _allProjects = new();
    private readonly ObservableCollection<ProjectInfo> _projects = new();
    private bool _isLoading;
    private string _searchText = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isStatusSuccess;

    public ProjectListViewModel(IProjectStore projectStore, INavigationService navigation)
    {
        _projectStore = projectStore;
        _navigation = navigation;
        Projects = new ReadOnlyObservableCollection<ProjectInfo>(_projects);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading);
        CreateCommand = new RelayCommand(CreateProject, () => !IsLoading);
        OpenCommand = new RelayCommand<ProjectInfo>(OpenProject);
    }

    public ReadOnlyObservableCollection<ProjectInfo> Projects { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand CreateCommand { get; }

    public IRelayCommand<ProjectInfo> OpenCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool HasProjects => _projects.Count > 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                CreateCommand.NotifyCanExecuteChanged();
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

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsStatusSuccess
    {
        get => _isStatusSuccess;
        private set => SetProperty(ref _isStatusSuccess, value);
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var projects = await _projectStore.GetAllAsync();
            _allProjects.Clear();
            foreach (var project in projects)
            {
                _allProjects.Add(project);
            }

            ApplyFilter();
            SetStatus($"{projects.Count} pekerjaan tersimpan.", true);
        }
        catch (Exception exception)
        {
            SetStatus($"Gagal memuat pekerjaan: {exception.Message}", false);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var search = SearchText.Trim();
        _projects.Clear();
        foreach (var project in _allProjects)
        {
            if (!string.IsNullOrWhiteSpace(search)
                && project.ShipName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                && project.JobName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                && project.RootPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            _projects.Add(project);
        }

        OnPropertyChanged(nameof(HasProjects));
    }

    private void CreateProject() => _navigation.NavigateTo<Views.Pages.ProjectCreatePage>();

    private void OpenProject(ProjectInfo? project)
    {
        if (project is not null)
        {
            _navigation.NavigateTo<Views.Pages.ProjectDetailPage>(project.Id);
        }
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        IsStatusSuccess = success;
    }
}
