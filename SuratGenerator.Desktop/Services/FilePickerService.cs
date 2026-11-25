using Microsoft.Win32;

namespace SuratGenerator.Desktop.Services;

internal sealed class FilePickerService : IFilePickerService
{
    public string? PickDocx()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih template Word",
            Filter = "Dokumen Word (*.docx)|*.docx",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickMasterFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih file master dokumen",
            Filter = FileFilter,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string> PickProjectFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih file untuk pekerjaan",
            Filter = FileFilter,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = true
        };

        return dialog.ShowDialog() == true ? dialog.FileNames.ToArray() : Array.Empty<string>();
    }

    public string? PickBackupArchive()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pilih arsip backup",
            Filter = "Arsip backup (*.zip)|*.zip",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private const string FileFilter =
        "File dokumen (*.docx;*.doc;*.xlsx;*.xls;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.docx;*.doc;*.xlsx;*.xls;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif";
}
