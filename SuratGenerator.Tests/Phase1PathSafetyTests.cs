using SuratGenerator.Core.Models;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase1PathSafetyTests
{
    [Fact]
    public async Task PengaturanPathTersimpanDanFolderDibuat()
    {
        var directory = CreateDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "workspace-paths.json");
            var service = new WorkspacePathService(settingsPath, directory);
            var master = Path.Combine(directory, "MasterDocuments");
            var projects = Path.Combine(directory, "Projects");
            var backups = Path.Combine(directory, "Backups");

            var saved = await service.SetAsync(master, projects, backups);

            Assert.Equal(master, saved.MasterDocumentsDirectory);
            Assert.Equal(projects, saved.ProjectsDirectory);
            Assert.Equal(backups, saved.BackupsDirectory);
            Assert.True(Directory.Exists(master));
            Assert.True(Directory.Exists(projects));
            Assert.True(Directory.Exists(backups));

            var reloaded = await new WorkspacePathService(settingsPath, directory).GetAsync();
            Assert.Equal(saved, reloaded);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task PengaturanPathMenolakLokasiTidakAman()
    {
        var directory = CreateDirectory();
        try
        {
            var service = new WorkspacePathService(Path.Combine(directory, "workspace-paths.json"), directory);
            var valid = Path.Combine(directory, "Projects");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SetAsync(string.Empty, valid, Path.Combine(directory, "Backups")));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SetAsync(valid, valid, Path.Combine(directory, "Backups")));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SetAsync(valid, Path.Combine(directory, "Projects"), Path.Combine(directory, "Projects", "Backups")));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void NamaFolderDanFileDisanitasiSesuaiAturanWindows()
    {
        var service = new WorkspacePathService(
            Path.Combine(Path.GetTempPath(), "unused-workspace.json"),
            Path.GetTempPath());

        Assert.Equal("KRI TBT 516 - Perbaikan Sistem Kelistrikan", service.BuildProjectFolderName(
            "KRI TBT - 516",
            "Perbaikan Sistem Kelistrikan"));
        Assert.Equal("_CON.txt", service.SanitizeFileName("CON.txt"));
        Assert.Equal("a_b_c", service.SanitizeFileName("a<b>c"));
        Assert.Equal("Laporan", service.SanitizeFileName("Laporan.   "));
        Assert.False(service.ValidateTargetDirectory("   ").IsValid);
        Assert.False(service.ValidateTargetDirectory("folder-relatif").IsValid);
    }

    [Fact]
    public void ValidasiPathMenolakFolderInstalasi()
    {
        var service = new WorkspacePathService(
            Path.Combine(Path.GetTempPath(), "unused-workspace.json"),
            Path.GetTempPath());
        var result = service.ValidateTargetDirectory(AppContext.BaseDirectory);

        Assert.False(result.IsValid);
        Assert.Contains("instalasi", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorV3Paths", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Cleanup(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
