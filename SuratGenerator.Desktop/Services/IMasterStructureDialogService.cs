namespace SuratGenerator.Desktop.Services;

internal interface IMasterStructureDialogService
{
    MasterStructureEditResult? Show(string? existingName);
}
