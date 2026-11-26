using System.Windows;
using System.Windows.Controls;
using SuratGenerator.Desktop.Helpers;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Views.Pages;

public partial class CatatanPage : Page
{
    private readonly CatatanViewModel _viewModel;

    public CatatanPage()
    {
        InitializeComponent();
        _viewModel = new CatatanViewModel(
            App.Notes,
            App.NoteItems,
            new NoteDialogService(),
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
