using System.Windows;
using SuratGenerator.Desktop.Views;

namespace SuratGenerator.Desktop.Services;

internal sealed class NoteDialogService : INoteDialogService
{
    public NoteEditResult? ShowNote(string? existingTitle)
    {
        var window = new NoteEditWindow(existingTitle)
        {
            Owner = Application.Current.MainWindow
        };

        return window.ShowDialog() == true ? window.Result : null;
    }

    public NoteItemEditResult? ShowItem(string? existingName, string? existingDescription)
    {
        var window = new NoteItemEditWindow(existingName, existingDescription)
        {
            Owner = Application.Current.MainWindow
        };

        return window.ShowDialog() == true ? window.Result : null;
    }
}
