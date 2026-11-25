using System.Windows.Media;
using SuratGenerator.Desktop.ViewModels;

namespace SuratGenerator.Desktop.Services;

internal sealed record PreviewPageViewModel(
    int PageNumber,
    ImageSource Image,
    double PageWidth,
    double PageHeight);
