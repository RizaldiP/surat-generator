using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media.Imaging;
using SuratGenerator.Desktop.ViewModels;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using WinBitmapEncoder = Windows.Graphics.Imaging.BitmapEncoder;

namespace SuratGenerator.Desktop.Services;

internal sealed class WindowsPdfPageRenderer : IPdfPageRenderer
{
    private const double RenderScale = 1.5;

    public async Task<IReadOnlyList<PreviewPageViewModel>> RenderAsync(
        string pdfPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("File pratinjau tidak ditemukan.", pdfPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var fileStream = File.OpenRead(pdfPath);
        using var sourceStream = fileStream.AsRandomAccessStream();
        var document = await PdfDocument.LoadFromStreamAsync(sourceStream);
        var pages = new List<PreviewPageViewModel>();

        for (uint index = 0; index < document.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = document.GetPage(index);
            var options = new PdfPageRenderOptions
            {
                BitmapEncoderId = WinBitmapEncoder.PngEncoderId,
                DestinationWidth = ToUint(page.Size.Width * RenderScale),
                DestinationHeight = ToUint(page.Size.Height * RenderScale)
            };

            using var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream, options);
            stream.Seek(0);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream.AsStreamForRead();
            image.EndInit();
            image.Freeze();

            pages.Add(new PreviewPageViewModel(
                (int)index + 1,
                image,
                options.DestinationWidth,
                options.DestinationHeight));
        }

        return pages;
    }

    private static uint ToUint(double value) => (uint)Math.Max(1, value);
}
