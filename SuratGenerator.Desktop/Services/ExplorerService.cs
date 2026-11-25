using System.Diagnostics;
using System.IO;

namespace SuratGenerator.Desktop.Services;

internal sealed class ExplorerService : IExplorerService
{
    public void OpenFile(string path)
    {
        var fullPath = Validate(path, file: true);
        Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
    }

    public void OpenFolder(string path)
    {
        var fullPath = Validate(path, file: false);
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, ArgumentList = { fullPath } });
    }

    public void RevealFile(string path)
    {
        var fullPath = Validate(path, file: true);
        Process.Start(new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true,
            ArgumentList = { $"/select,{fullPath}" }
        });
    }

    private static string Validate(string path, bool file)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path tidak boleh kosong.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        var exists = file ? File.Exists(fullPath) : Directory.Exists(fullPath);
        if (!exists)
        {
            throw file
                ? new FileNotFoundException("File tidak ditemukan.", fullPath)
                : new DirectoryNotFoundException($"Folder tidak ditemukan: {fullPath}");
        }

        return fullPath;
    }
}
