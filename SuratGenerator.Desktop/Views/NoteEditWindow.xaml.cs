using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Desktop.Services;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop.Views;

public partial class NoteEditWindow : FluentWindow
{
    public NoteEditWindow(string? existingTitle)
    {
        InitializeComponent();
        SaveCommand = new RelayCommand(Save);
        NoteTitle = existingTitle ?? string.Empty;
        WindowTitle = existingTitle is null ? "Tambah Catatan" : "Edit Catatan";
        DataContext = this;
    }

    public string WindowTitle { get; }

    public string NoteTitle { get; set; } = string.Empty;

    public NoteEditResult? Result { get; private set; }

    public IRelayCommand SaveCommand { get; }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(NoteTitle))
        {
            System.Windows.MessageBox.Show(
                "Judul catatan tidak boleh kosong.",
                "Validasi",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        Result = new NoteEditResult(NoteTitle.Trim());
        DialogResult = true;
        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Save();
            e.Handled = true;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
