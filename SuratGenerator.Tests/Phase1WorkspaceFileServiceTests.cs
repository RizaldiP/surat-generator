using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase1WorkspaceFileServiceTests
{
    [Fact]
    public async Task PenyalinanFileMendukungOverwriteBackupDanNamaUnik()
    {
        var directory = CreateDirectory();
        try
        {
            var root = Path.Combine(directory, "Projects");
            Directory.CreateDirectory(root);
            var sourcePath = Path.Combine(directory, "sumber.docx");
            await File.WriteAllTextAsync(sourcePath, "isi lama");
            var relative = Path.Combine("kategori", "file.docx");
            var service = new WorkspaceFileService();

            var copied = await service.CopyAsync(sourcePath, root, relative, false);
            Assert.Equal("isi lama", await File.ReadAllTextAsync(copied));

            await Assert.ThrowsAsync<IOException>(() => service.CopyAsync(sourcePath, root, relative, false));

            var unique = await service.EnsureUniquePathAsync(root, relative);
            Assert.EndsWith("file (2).docx", unique, StringComparison.Ordinal);

            await File.WriteAllTextAsync(sourcePath, "isi baru");
            var replaced = await service.CopyWithBackupAsync(sourcePath, root, relative);
            Assert.Equal("isi baru", await File.ReadAllTextAsync(replaced));
            var backups = Directory.GetFiles(root, "file.docx.bak-*", SearchOption.AllDirectories);
            Assert.Single(backups);
            Assert.Equal("isi lama", await File.ReadAllTextAsync(backups[0]));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task StagingDapatDFinalisasiDanDibersihkanDenganAman()
    {
        var directory = CreateDirectory();
        try
        {
            var root = Path.Combine(directory, "Projects");
            Directory.CreateDirectory(root);
            var service = new WorkspaceFileService();

            var staging = await service.CreateStagingDirectoryAsync(root, "KRI TBT 516");
            var stagedFile = Path.Combine(staging, "faktur.docx");
            await File.WriteAllTextAsync(stagedFile, "isi");
            var final = Path.Combine(root, "KRI TBT 516", "01. Dokumen Tagihan");

            await service.FinalizeStagingAsync(staging, final, root);

            Assert.True(File.Exists(Path.Combine(final, "faktur.docx")));
            Assert.False(Directory.Exists(staging));

            var outside = Path.Combine(directory, "luar");
            Directory.CreateDirectory(outside);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CleanupStagingAsync(outside, root));
            Assert.True(Directory.Exists(outside));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task PenyalinanMenolakPathRelatifKeluarRoot()
    {
        var directory = CreateDirectory();
        try
        {
            var root = Path.Combine(directory, "Projects");
            Directory.CreateDirectory(root);
            var sourcePath = Path.Combine(directory, "sumber.docx");
            await File.WriteAllTextAsync(sourcePath, "isi");
            var service = new WorkspaceFileService();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.CopyAsync(sourcePath, root, Path.Combine("..", "luar.docx"), false));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorV3Files", Guid.NewGuid().ToString("N"));
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
