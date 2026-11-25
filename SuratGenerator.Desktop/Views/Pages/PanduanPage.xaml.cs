using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class PanduanPage : Page
{
    private readonly PanduanViewModel _viewModel;

    public PanduanPage()
    {
        InitializeComponent();
        _viewModel = new PanduanViewModel(App.Navigation);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
    }
}
