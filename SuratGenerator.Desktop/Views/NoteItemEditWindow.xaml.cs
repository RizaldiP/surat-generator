using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Desktop.Services;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop.Views;

public partial class NoteItemEditWindow : FluentWindow
{
    public NoteItemEditWindow(string? existingName, string? existingDescription)
    {
        InitializeComponent();
        SaveCommand = new RelayCommand(Save);
        ItemName = existingName ?? string.Empty;
        ItemDescription = existingDescription ?? string.Empty;
        WindowTitle = existingName is null ? "Tambah Syarat" : "Edit Syarat";
        DataContext = this;
    }

    public string WindowTitle { get; }

    public string ItemName { get; set; } = string.Empty;

    public string ItemDescription { get; set; } = string.Empty;

    public NoteItemEditResult? Result { get; private set; }

    public IRelayCommand SaveCommand { get; }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ItemName))
        {
            System.Windows.MessageBox.Show(
                "Nama syarat tidak boleh kosong.",
                "Validasi",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        Result = new NoteItemEditResult(ItemName.Trim(), ItemDescription.Trim());
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
