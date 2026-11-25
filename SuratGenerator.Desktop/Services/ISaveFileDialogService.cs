using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.Services;

internal interface ISaveFileDialogService
{
    string? PickSavePath(GeneratedDocumentKind kind, string suggestedFileName);
}
