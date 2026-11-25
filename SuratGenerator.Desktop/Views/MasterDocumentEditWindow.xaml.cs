using System.Windows;
using SuratGenerator.Desktop.Services;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop.Views;

public partial class MasterDocumentEditWindow : FluentWindow
{
    public MasterDocumentEditWindow(string displayName, bool isRequired, bool isNew)
    {
        InitializeComponent();
        DisplayName = displayName;
        IsRequired = isRequired;
        WindowTitle = isNew ? "Tambah Master Dokumen" : "Edit Master Dokumen";
        DataContext = this;
    }

    public string WindowTitle { get; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public MasterDocumentEditResult? Result { get; private set; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            System.Windows.MessageBox.Show(
                "Nama tampilan tidak boleh kosong.",
                "Validasi",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        Result = new MasterDocumentEditResult(DisplayName.Trim(), IsRequired);
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
