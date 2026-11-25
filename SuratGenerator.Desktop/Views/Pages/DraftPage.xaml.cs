using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Infrastructure.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class DraftPage : Page
{
    private readonly DraftViewModel _viewModel;

    public DraftPage()
    {
        InitializeComponent();
        _viewModel = new DraftViewModel(new DraftStore(), App.Navigation);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.LoadAsync();
    }
}
