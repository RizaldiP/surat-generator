using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Services;

internal interface IPdfPageRenderer
{
    Task<IReadOnlyList<PreviewPageViewModel>> RenderAsync(
        string pdfPath,
        CancellationToken cancellationToken = default);
}
