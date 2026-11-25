using System.Windows;
using SuratGenerator.Core.Models;
using SuratGenerator.Desktop.Views;

namespace SuratGenerator.Desktop.Services;

internal sealed class TemplateEditDialogService : ITemplateEditDialogService
{
    public TemplateEditResult? Show(TemplateInfo template)
    {
        var window = new TemplateEditWindow(template)
        {
            Owner = Application.Current.MainWindow
        };

        return window.ShowDialog() == true ? window.Result : null;
    }
}
