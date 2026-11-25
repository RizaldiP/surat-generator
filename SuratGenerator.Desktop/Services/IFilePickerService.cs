namespace SuratGenerator.Desktop.Services;

internal interface IFilePickerService
{
    string? PickDocx();

    string? PickMasterFile();

    IReadOnlyList<string> PickProjectFiles();

    string? PickBackupArchive();
}
