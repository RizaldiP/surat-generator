namespace SuratGenerator.Desktop.Services;

internal interface IFolderPickerService
{
    string? PickFolder(string? initialPath = null);
}
