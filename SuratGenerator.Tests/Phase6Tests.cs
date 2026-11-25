using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase6Tests
{
    private readonly ITemplateSyncDecisionService _decisionService = new TemplateSyncDecisionService();

    [Theory]
    [InlineData("https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWxYz012345", "1AbCdEfGhIjKlMnOpQrStUvWxYz012345")]
    [InlineData("https://drive.google.com/drive/u/0/folders/ABCDEFGHIJKLMNOPQRSTUVWXYZ012345", "ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")]
    [InlineData("https://drive.google.com/open?id=ABCDEFGHIJKLMNOPQRSTUVWXYZ012345", "ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ012345", "ABCDEFGHIJKLMNOPQRSTUVWXYZ012345")]
    public void MemotongFolderIdDariBerbagaiBentukLink(string input, string expected)
    {
        Assert.True(GoogleDriveFolderIdParser.TryExtract(input, out var folderId));
        Assert.Equal(expected, folderId);
    }

    [Fact]
    public void LinkTanpaFolderIdDitolak()
    {
        Assert.False(GoogleDriveFolderIdParser.TryExtract("https://example.com/folders/abc", out _));
    }

    [Fact]
    public void TemplateRemoteBerubahDanLokalTetapDiunduh()
    {
        var decision = _decisionService.Decide(CreateSnapshot(
            remoteChecksum: "remote-baru",
            localChecksum: "checksum-lama",
            lastSyncedChecksum: "checksum-lama"));

        Assert.Equal(TemplateSyncDecision.Download, decision);
    }

    [Fact]
    public void TemplateLokalBerubahDanRemoteTetapDiunggah()
    {
        var decision = _decisionService.Decide(CreateSnapshot(
            remoteChecksum: "checksum-lama",
            localChecksum: "lokal-baru",
            lastSyncedChecksum: "checksum-lama"));

        Assert.Equal(TemplateSyncDecision.Upload, decision);
    }

    [Fact]
    public void PerubahanDiKeduaSisiMenjadiKonflik()
    {
        var decision = _decisionService.Decide(CreateSnapshot(
            remoteChecksum: "remote-baru",
            localChecksum: "lokal-baru",
            lastSyncedChecksum: "checksum-lama"));

        Assert.Equal(TemplateSyncDecision.Conflict, decision);
    }

    [Fact]
    public void ChecksumSamaTidakMengunduhUlang()
    {
        var decision = _decisionService.Decide(CreateSnapshot(
            remoteChecksum: "checksum-sama",
            localChecksum: "checksum-sama",
            lastSyncedChecksum: "checksum-sama"));

        Assert.Equal(TemplateSyncDecision.Skip, decision);
    }

    [Fact]
    public void TemplateDihapusLokalDiabaikanSaatSinkronisasi()
    {
        var snapshot = CreateSnapshot(
            remoteChecksum: "remote-baru",
            localChecksum: "checksum-lama",
            lastSyncedChecksum: "checksum-lama") with { IsLocallyDeleted = true };

        Assert.Equal(TemplateSyncDecision.Ignore, _decisionService.Decide(snapshot));
    }

    [Fact]
    public void DatabaseLamaMendapatTabelStatusSinkronisasi()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");

        try
        {
            using (var store = new TemplateStore(CreateOptions(databasePath), Path.Combine(directory, "Templates")))
            {
                Assert.NotNull(store);
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'DriveSyncStates';";
            Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileTemplateDapatDigantiTanpaMengubahIdentitas()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var sourcePath = Path.Combine(directory, "surat.docx");
        var replacementPath = Path.Combine(directory, "baru.docx");
        await File.WriteAllTextAsync(sourcePath, "dummy lama");
        await File.WriteAllTextAsync(replacementPath, "dummy baru");

        try
        {
            using var store = new TemplateStore(
                CreateOptions(databasePath),
                Path.Combine(directory, "Templates"));
            var original = await store.SaveAsync(
                sourcePath,
                new[] { new TemplateField("nama", "Nama", 1, TemplateFieldKind.Text) });
            await store.UpdateMetadataAsync(original.Id, "Surat", "Umum", "1.0");

            var replaced = await store.ReplaceFileAsync(
                original.Id,
                replacementPath,
                new[] { new TemplateField("alamat", "Alamat", 1, TemplateFieldKind.Text) },
                "baru.docx");

            Assert.Equal(original.Id, replaced.Id);
            Assert.Equal("baru.docx", replaced.SourceFileName);
            Assert.Equal("alamat", Assert.Single(replaced.Fields).Key);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static TemplateSyncSnapshot CreateSnapshot(
        string remoteChecksum,
        string localChecksum,
        string lastSyncedChecksum) =>
        new(
            Guid.NewGuid(),
            "Template",
            "1.0",
            localChecksum,
            DateTimeOffset.UtcNow,
            "remote-id",
            "Template.docx",
            "1.0",
            remoteChecksum,
            DateTimeOffset.UtcNow,
            lastSyncedChecksum,
            "1.0",
            false,
            true);

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<SuratGeneratorDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
    }
}
