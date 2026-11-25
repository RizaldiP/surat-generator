using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class ProjectListPage : Page
{
    private readonly ProjectListViewModel _viewModel;

    public ProjectListPage()
    {
        InitializeComponent();
        _viewModel = new ProjectListViewModel(App.Projects, App.Navigation);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.LoadAsync();
    }
}
