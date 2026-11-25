using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class ProjectCreatePage : Page
{
    private readonly ProjectCreateViewModel _viewModel;

    public ProjectCreatePage()
    {
        InitializeComponent();
        _viewModel = new ProjectCreateViewModel(
            App.Projects,
            App.WorkspacePaths,
            App.Navigation,
            App.ProjectDialogs);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.InitializeAsync();
    }
}
