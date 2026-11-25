using System.IO;
using Microsoft.Win32;

namespace SuratGenerator.Desktop.Services;

internal sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialPath = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pilih folder template",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
