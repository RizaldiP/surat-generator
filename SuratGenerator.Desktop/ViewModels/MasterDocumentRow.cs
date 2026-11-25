using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.ViewModels;

public sealed record MasterDocumentRow(
    MasterDocumentInfo Document,
    string CategoryName,
    string VersionText,
    string SizeText,
    string StatusText);
