using System.IO;
using Microsoft.Win32;
using SuratGenerator.Core.Models;
using SuratGenerator.Infrastructure;

namespace SuratGenerator.Desktop.Services;

internal sealed class SaveFileDialogService : ISaveFileDialogService
{
    public string? PickSavePath(GeneratedDocumentKind kind, string suggestedFileName)
    {
        Directory.CreateDirectory(AppPaths.ExportDirectory);

        var isWord = kind == GeneratedDocumentKind.Docx;
        var dialog = new SaveFileDialog
        {
            Title = isWord ? "Simpan dokumen Word" : "Simpan dokumen PDF",
            Filter = isWord
                ? "Dokumen Word (*.docx)|*.docx"
                : "Dokumen PDF (*.pdf)|*.pdf",
            FileName = Sanitize(suggestedFileName),
            InitialDirectory = AppPaths.ExportDirectory,
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Surat" : cleaned;
    }
}
