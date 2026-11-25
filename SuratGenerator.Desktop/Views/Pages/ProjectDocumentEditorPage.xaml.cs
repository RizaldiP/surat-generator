using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class ProjectDocumentEditorPage : Page
{
    private readonly DocumentEditorViewModel _viewModel;

    public ProjectDocumentEditorPage()
    {
        InitializeComponent();
        var documentId = App.Navigation.TakeParameter() is Guid id ? id : Guid.Empty;
        _viewModel = new DocumentEditorViewModel(
            App.Projects,
            App.DocumentFieldValues,
            App.PlaceholderAliases,
            App.TemplateReader,
            App.WordPdfRenderer,
            new WindowsPdfPageRenderer(),
            new ConfirmationService(),
            App.Navigation,
            App.Logger);
        _viewModel.Initialize(documentId);
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.PlayEntrance(this);
        await _viewModel.LoadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Dispose();
    }
}
