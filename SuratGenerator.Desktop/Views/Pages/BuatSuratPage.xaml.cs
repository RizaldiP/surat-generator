using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class BuatSuratPage : Page
{
    private readonly BuatSuratViewModel _viewModel;
    private readonly Guid? _draftIdToOpen;

    public BuatSuratPage()
    {
        InitializeComponent();
        var parameter = App.Navigation.TakeParameter();
        _draftIdToOpen = parameter is Guid draftId ? draftId : (Guid?)null;
        _viewModel = new BuatSuratViewModel(
            App.TemplateFolder,
            new TemplateStore(App.Logger),
            App.WordPdfRenderer,
            new WindowsPdfPageRenderer(),
            new DraftStore(),
            new GeneratedDocumentStore(),
            new SaveFileDialogService(),
            new ConfirmationService());
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        try
        {
            await _viewModel.ScanFolderAsync();
            if (_draftIdToOpen is Guid draftId)
            {
                await _viewModel.LoadDraftAsync(draftId);
            }
        }
        catch (Exception exception)
        {
            App.Logger.Error("Gagal menyiapkan halaman Buat Surat.", exception);
            _viewModel.ReportLoadFailure("Gagal membuka halaman Buat Surat. Silakan coba lagi.");
        }
    }
}
