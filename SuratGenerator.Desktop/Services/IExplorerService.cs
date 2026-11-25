namespace SuratGenerator.Desktop.Services;

internal interface IExplorerService
{
    void OpenFile(string path);

    void OpenFolder(string path);

    void RevealFile(string path);
}
