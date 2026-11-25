using System.Windows;
using SuratGenerator.Desktop.Services;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop.Views;

public partial class MasterStructureEditWindow : FluentWindow
{
    public MasterStructureEditWindow(string? existingName)
    {
        InitializeComponent();
        CategoryName = existingName ?? string.Empty;
        WindowTitle = existingName is null ? "Tambah Kategori" : "Edit Kategori";
        DataContext = this;
    }

    public string WindowTitle { get; }

    public string CategoryName { get; set; } = string.Empty;

    public MasterStructureEditResult? Result { get; private set; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CategoryName))
        {
            System.Windows.MessageBox.Show(
                "Nama kategori tidak boleh kosong.",
                "Validasi",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        Result = new MasterStructureEditResult(CategoryName.Trim());
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
