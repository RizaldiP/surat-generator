using System.Windows;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.Services;

internal sealed class ProjectDialogService : IProjectDialogService
{
    public DuplicateProjectDecision ShowDuplicate(ProjectDuplicateInfo duplicate)
    {
        var result = MessageBox.Show(
            $"Pekerjaan '{duplicate.ShipName} - {duplicate.JobName}' sudah ada.\n\nFolder: {duplicate.RootPath}\n\nPilih Ya untuk membuka pekerjaan lama, Tidak untuk memakai nama berbeda, atau Batal.",
            "Pekerjaan Duplikat",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);

        return result switch
        {
            MessageBoxResult.Yes => DuplicateProjectDecision.OpenExisting,
            MessageBoxResult.No => DuplicateProjectDecision.UseDifferentName,
            _ => DuplicateProjectDecision.Cancel
        };
    }

    public ProjectUploadCollisionPolicy ShowFileCollision(ProjectFileCollisionInfo collision)
    {
        var result = MessageBox.Show(
            $"File '{collision.FileName}' sudah ada di folder pekerjaan.\n\n{ExistingPathText(collision.ExistingPath)}\n\nYa: ganti dengan backup\nTidak: simpan dengan nama lain\nBatal: lewati file ini",
            "File Duplikat",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);

        return result switch
        {
            MessageBoxResult.Yes => ProjectUploadCollisionPolicy.ReplaceWithBackup,
            MessageBoxResult.No => ProjectUploadCollisionPolicy.SaveAsDifferentName,
            _ => ProjectUploadCollisionPolicy.Cancel
        };
    }

    private static string ExistingPathText(string path) => $"Lokasi: {path}";
}
