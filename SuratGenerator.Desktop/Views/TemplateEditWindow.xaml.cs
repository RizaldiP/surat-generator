using System.Windows;
using SuratGenerator.Core.Models;
using SuratGenerator.Desktop.Services;
using Wpf.Ui.Controls;

namespace SuratGenerator.Desktop.Views;

public partial class TemplateEditWindow : FluentWindow
{
    public TemplateEditWindow(TemplateInfo template)
    {
        InitializeComponent();
        TemplateName = template.Name;
        Category = template.Category;
        VersionText = template.Version;
        DataContext = this;
    }

    public string TemplateName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string VersionText { get; set; } = string.Empty;

    public TemplateEditResult? Result { get; private set; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            System.Windows.MessageBox.Show(
                "Nama template tidak boleh kosong.",
                "Validasi",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        Result = new TemplateEditResult(TemplateName.Trim(), Category.Trim(), VersionText.Trim());
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
