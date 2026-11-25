using System.Windows;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;
using SuratGenerator.Desktop.Views.Pages;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop;

public partial class MainWindow : FluentWindow
{
    private readonly AppThemeService _themeService = new();
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _themeService.Initialize(this);
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigationView.ApplyTemplate();
        App.Navigation.Register(pageType => RootNavigationView.Navigate(pageType));
        RootNavigationView.Navigate(typeof(BuatSuratPage));
        AnimationHelper.PlayEntrance(RootNavigationView);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RootNavigationView.PaneDisplayMode = e.NewSize.Width < 1000
            ? NavigationViewPaneDisplayMode.LeftMinimal
            : NavigationViewPaneDisplayMode.Left;
    }

    protected override void OnClosed(EventArgs e)
    {
        App.Navigation.Unregister();
        _viewModel.Dispose();
        _themeService.Dispose();
        base.OnClosed(e);
    }
}
