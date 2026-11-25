using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class TemplatePage : Page
{
    private readonly TemplateViewModel _viewModel;

    public TemplatePage()
    {
        InitializeComponent();
        _viewModel = new TemplateViewModel(
            new TemplateStore(App.Logger),
            new TemplateEditDialogService(),
            new ConfirmationService(),
            App.TemplateFolder);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.ScanFolderAsync();
    }
}
