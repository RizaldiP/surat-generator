using System.Windows;
using SuratGenerator.Desktop.Views;

namespace SuratGenerator.Desktop.Services;

internal sealed class MasterDocumentDialogService : IMasterDocumentDialogService
{
    public MasterDocumentEditResult? Show(string displayName, bool isRequired, bool isNew)
    {
        var window = new MasterDocumentEditWindow(displayName, isRequired, isNew)
        {
            Owner = Application.Current.MainWindow
        };

        return window.ShowDialog() == true ? window.Result : null;
    }
}
