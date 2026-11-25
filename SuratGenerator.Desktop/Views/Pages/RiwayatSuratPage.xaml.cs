using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.ViewModels;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class RiwayatSuratPage : Page
{
    private readonly RiwayatSuratViewModel _viewModel;

    public RiwayatSuratPage()
    {
        InitializeComponent();
        _viewModel = new RiwayatSuratViewModel(new GeneratedDocumentStore());
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.LoadAsync();
    }
}
