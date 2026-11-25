namespace SuratGenerator.Desktop.Services;

internal interface IMasterDocumentDialogService
{
    MasterDocumentEditResult? Show(string displayName, bool isRequired, bool isNew);
}
