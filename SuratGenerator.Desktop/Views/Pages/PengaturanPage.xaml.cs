using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class PengaturanPage : Page
{
    private readonly PengaturanViewModel _viewModel;

    public PengaturanPage()
    {
        InitializeComponent();
        _viewModel = new PengaturanViewModel(
            App.TemplateFolder,
            new FolderPickerService(),
            App.FilePicker,
            App.TemplateFolderWatcher,
            App.WorkspacePaths,
            App.MasterDocuments,
            new ConfirmationService(),
            App.BackupService,
            App.BackupRecords,
            App.Projects);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.InitializeAsync();
    }
}
