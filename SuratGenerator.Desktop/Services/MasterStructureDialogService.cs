using System.Windows;
using SuratGenerator.Desktop.Views;

namespace SuratGenerator.Desktop.Services;

internal sealed class MasterStructureDialogService : IMasterStructureDialogService
{
    public MasterStructureEditResult? Show(string? existingName)
    {
        var window = new MasterStructureEditWindow(existingName)
        {
            Owner = Application.Current.MainWindow
        };

        return window.ShowDialog() == true ? window.Result : null;
    }
}
