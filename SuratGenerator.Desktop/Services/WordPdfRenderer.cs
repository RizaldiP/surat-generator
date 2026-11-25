using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Documents.Models;
using SuratGenerator.Documents.Services;
using SuratGenerator.Infrastructure;

namespace SuratGenerator.Desktop.Services;

internal sealed class WordPdfRenderer : IWordDocumentService, ILegacyTemplateConverter, IDisposable
{
    private const int WdAlertsNone = 0;
    private const int WdFindContinue = 1;
    private const int WdReplaceAll = 2;
    private const int WdDoNotSaveChanges = 0;
    private const int WdExportFormatPdf = 17;
    private const int WdExportOptimizeForPrint = 0;
    private const int WdExportAllDocument = 0;
    private const int WdExportDocumentContent = 0;
    private const int WdExportCreateNoBookmarks = 0;
    private const int WdFormatDocumentDefault = 16;

    private readonly Dispatcher _dispatcher;
    private readonly IAppLogger _logger;
    private object? _application;
    private bool _disposed;

    public WordPdfRenderer(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "SuratGenerator.WordPreview"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        while (dispatcher is null)
        {
            Thread.Sleep(1);
        }

        _dispatcher = dispatcher;
        CleanupStaleDirectories();
    }

    public Task<string> RenderAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return _dispatcher.InvokeAsync(() => RenderCore(templatePath, values, cancellationToken)).Task;
    }

    public Task ExportAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        string outputPath,
        GeneratedDocumentKind kind,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        return kind switch
        {
            GeneratedDocumentKind.Pdf => ExportPdfAsync(templatePath, values, outputPath, cancellationToken),
            GeneratedDocumentKind.Docx => _dispatcher.InvokeAsync(
                () => ExportDocxCore(templatePath, values, outputPath, cancellationToken)).Task,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public Task PrintAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return _dispatcher.InvokeAsync(() => PrintCore(templatePath, values, cancellationToken)).Task;
    }

    public Task<TemplateConversionResult> ConvertToDocxAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return _dispatcher.InvokeAsync(
            () => ConvertToDocxCore(sourcePath, cancellationToken)).Task;
    }

    public void Cleanup(string pdfPath)
    {
        var directory = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            DeleteDirectory(directory);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatcher.Invoke(ShutdownApplication);
        CleanupAllDirectories();
        _dispatcher.InvokeShutdown();
    }

    private string RenderCore(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("File template tidak ditemukan.", templatePath);
        }

        Directory.CreateDirectory(AppPaths.PreviewDirectory);
        var directory = Path.Combine(AppPaths.PreviewDirectory, $"preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var documentPath = Path.Combine(directory, "template.docx");
        var pdfPath = Path.Combine(directory, "preview.pdf");

        try
        {
            File.Copy(templatePath, documentPath, true);
            dynamic application = GetApplication();
            dynamic documents = application.Documents;
            object? documentObject = null;

            try
            {
                dynamic document = documents.Open(documentPath, false, true, false);
                documentObject = document;
                ReplacePlaceholders(document, values, cancellationToken);
                ExportPdf(document, pdfPath);
                return pdfPath;
            }
            finally
            {
                CloseDocument(documentObject);
                ReleaseComObject(documents);
            }
        }
        catch
        {
            DeleteDirectory(directory);
            throw;
        }
    }

    private async Task ExportPdfAsync(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var previewPath = await RenderAsync(templatePath, values, cancellationToken);
        try
        {
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.Copy(previewPath, outputPath, true);
        }
        finally
        {
            Cleanup(previewPath);
        }
    }

    private void ExportDocxCore(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        string outputPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("File template tidak ditemukan.", templatePath);
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        Directory.CreateDirectory(AppPaths.PreviewDirectory);
        var directory = Path.Combine(AppPaths.PreviewDirectory, $"export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var workingPath = Path.Combine(directory, "template.docx");

        try
        {
            File.Copy(templatePath, workingPath, true);
            dynamic application = GetApplication();
            dynamic documents = application.Documents;
            object? documentObject = null;

            try
            {
                dynamic document = documents.Open(workingPath, false, true, false);
                documentObject = document;
                ReplacePlaceholders(document, values, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                document.SaveAs2(outputPath, WdFormatDocumentDefault);
            }
            finally
            {
                CloseDocument(documentObject);
                ReleaseComObject(documents);
            }
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private void PrintCore(
        string templatePath,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("File template tidak ditemukan.", templatePath);
        }

        Directory.CreateDirectory(AppPaths.PreviewDirectory);
        var directory = Path.Combine(AppPaths.PreviewDirectory, $"print-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var workingPath = Path.Combine(directory, "template.docx");

        try
        {
            File.Copy(templatePath, workingPath, true);
            dynamic application = GetApplication();
            dynamic documents = application.Documents;
            object? documentObject = null;

            try
            {
                dynamic document = documents.Open(workingPath, false, true, false);
                documentObject = document;
                ReplacePlaceholders(document, values, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                document.PrintOut(false);
            }
            finally
            {
                CloseDocument(documentObject);
                ReleaseComObject(documents);
            }
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private TemplateConversionResult ConvertToDocxCore(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(sourcePath))
        {
            return TemplateConversionResult.Failure("File template tidak ditemukan.");
        }

        Directory.CreateDirectory(AppPaths.PreviewDirectory);
        var directory = Path.Combine(AppPaths.PreviewDirectory, $"convert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, "converted.docx");
        object? documentObject = null;
        var converted = false;

        try
        {
            dynamic application = GetApplication();
            dynamic documents = application.Documents;
            try
            {
                dynamic document = documents.Open(sourcePath, false, true, false);
                documentObject = document;
                cancellationToken.ThrowIfCancellationRequested();
                document.SaveAs2(outputPath, WdFormatDocumentDefault);
                converted = File.Exists(outputPath);
                return converted
                    ? TemplateConversionResult.Success(outputPath)
                    : TemplateConversionResult.Failure("Gagal menyimpan hasil konversi .doc.");
            }
            finally
            {
                CloseDocument(documentObject);
                ReleaseComObject(documents);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or System.Runtime.InteropServices.COMException)
        {
            return TemplateConversionResult.Failure(
                $"Gagal mengonversi .doc: {exception.Message}");
        }
        finally
        {
            if (!converted)
            {
                DeleteDirectory(directory);
            }
        }
    }

    private object GetApplication()
    {
        if (_application is null)
        {
            var type = Type.GetTypeFromProgID("Word.Application", true)
                ?? throw new InvalidOperationException("Microsoft Word tidak terpasang.");
            dynamic application = Activator.CreateInstance(type)!;
            application.Visible = false;
            application.DisplayAlerts = WdAlertsNone;
            _application = application;
        }

        return _application;
    }

    private static void ReplacePlaceholders(
        dynamic document,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        foreach (var pair in values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            object? content = null;
            object? find = null;
            try
            {
                content = document.Content;
                find = ((dynamic)content!).Find;
                ((dynamic)find!).Execute(
                    $"{{{pair.Key}}}",
                    false,
                    false,
                    false,
                    false,
                    false,
                    true,
                    WdFindContinue,
                    false,
                    pair.Value ?? string.Empty,
                    WdReplaceAll,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing);
            }
            finally
            {
                ReleaseComObject(find);
                ReleaseComObject(content);
            }
        }
    }

    private static void ExportPdf(dynamic document, string pdfPath)
    {
        document.ExportAsFixedFormat(
            pdfPath,
            WdExportFormatPdf,
            false,
            WdExportOptimizeForPrint,
            WdExportAllDocument,
            0,
            0,
            WdExportDocumentContent,
            false,
            false,
            WdExportCreateNoBookmarks,
            true,
            true,
            false,
            Type.Missing);
    }

    private void CloseDocument(object? document)
    {
        if (document is null)
        {
            return;
        }

        try
        {
            ((dynamic)document).Close(WdDoNotSaveChanges);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Gagal menutup dokumen Word: {ex.Message}");
        }
        finally
        {
            ReleaseComObject(document);
        }
    }

    private void ShutdownApplication()
    {
        var application = _application;
        if (application is null)
        {
            return;
        }

        try
        {
            ((dynamic)application).Quit(WdDoNotSaveChanges);
        }
        catch (Exception ex)
        {
            _logger.Error("Gagal menghentikan aplikasi Word COM.", ex);
        }
        finally
        {
            ReleaseComObject(application);
            _application = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null)
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(value);
        }
        catch (Exception)
        {
        }
    }

    private static void CleanupAllDirectories()
    {
        if (!Directory.Exists(AppPaths.PreviewDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateDirectories(AppPaths.PreviewDirectory))
        {
            DeleteDirectory(path);
        }
    }

    private static void CleanupStaleDirectories()
    {
        if (!Directory.Exists(AppPaths.PreviewDirectory))
        {
            return;
        }

        var threshold = DateTime.UtcNow.AddMinutes(-10);
        foreach (var path in Directory.EnumerateDirectories(AppPaths.PreviewDirectory))
        {
            try
            {
                var directory = new DirectoryInfo(path);
                if (directory.LastWriteTimeUtc < threshold)
                {
                    directory.Delete(true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void DeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }

                return;
            }
            catch (IOException)
            {
                if (attempt == 2)
                {
                    return;
                }

                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
