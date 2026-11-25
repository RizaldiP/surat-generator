using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class MasterDocumentPage : Page
{
    private readonly MasterDocumentViewModel _viewModel;

    public MasterDocumentPage()
    {
        InitializeComponent();
        _viewModel = new MasterDocumentViewModel(
            App.MasterDocuments,
            App.DocumentCategories,
            App.WorkspacePaths,
            new FilePickerService(),
            new MasterDocumentDialogService(),
            new ConfirmationService());
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.LoadAsync();
    }
}
