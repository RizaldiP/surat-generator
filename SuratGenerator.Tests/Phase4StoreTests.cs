using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase4StoreTests
{
    [Fact]
    public async Task MenyimpanDanMemperbaruiDraftSebagaiJson()
    {
        var directory = CreateDirectory();
        var options = CreateOptions(directory);
        try
        {
            var id = Guid.NewGuid();
            var templateId = Guid.NewGuid();
            var firstValues = new Dictionary<string, string>
            {
                ["nama"] = "PT Contoh",
                ["tanggal"] = "23 November 2026"
            };

            using (var store = new DraftStore(options))
            {
                var saved = await store.SaveAsync(new DraftInfo(
                    id,
                    templateId,
                    "Template Contoh",
                    "Draft A",
                    firstValues,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));

                Assert.Equal(id, saved.Id);

                var updated = await store.SaveAsync(new DraftInfo(
                    id,
                    templateId,
                    "Template Contoh",
                    "Draft A",
                    new Dictionary<string, string> { ["nama"] = "PT Update" },
                    saved.CreatedAt,
                    saved.UpdatedAt));

                Assert.Equal(saved.CreatedAt, updated.CreatedAt);
                Assert.True(updated.UpdatedAt >= saved.UpdatedAt);
            }

            using (var store = new DraftStore(options))
            {
                var all = await store.GetAllAsync();
                var loaded = await store.GetAsync(id);

                Assert.Single(all);
                Assert.NotNull(loaded);
                Assert.Equal("Template Contoh", loaded!.TemplateName);
                Assert.Equal("PT Update", loaded.Values["nama"]);
                Assert.False(loaded.Values.ContainsKey("tanggal"));
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task MenyimpanDanMembacaRiwayatDokumen()
    {
        var directory = CreateDirectory();
        var options = CreateOptions(directory);
        try
        {
            var templateId = Guid.NewGuid();
            using (var store = new GeneratedDocumentStore(options))
            {
                await store.RecordAsync(new GeneratedDocumentInfo(
                    Guid.NewGuid(),
                    templateId,
                    "Template Contoh",
                    "surat.docx",
                    GeneratedDocumentKind.Docx,
                    Path.Combine(directory, "surat.docx"),
                    2048,
                    DateTimeOffset.UtcNow.AddMinutes(-1)));

                await store.RecordAsync(new GeneratedDocumentInfo(
                    Guid.NewGuid(),
                    templateId,
                    "Template Contoh",
                    "surat - Cetak",
                    GeneratedDocumentKind.Print,
                    string.Empty,
                    0,
                    DateTimeOffset.UtcNow));
            }

            using (var store = new GeneratedDocumentStore(options))
            {
                var documents = await store.GetAllAsync();

                Assert.Equal(2, documents.Count);
                Assert.Equal(GeneratedDocumentKind.Print, documents[0].Kind);
                Assert.Equal("Cetak", documents[0].FormatLabel);
                Assert.False(documents[0].HasFile);
                Assert.Equal(GeneratedDocumentKind.Docx, documents[1].Kind);
                Assert.True(documents[1].HasFile);
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<SuratGenerator.Infrastructure.Data.SuratGeneratorDbContext> CreateOptions(
        string directory)
    {
        var databasePath = Path.Combine(directory, "test.db");
        return new DbContextOptionsBuilder<SuratGenerator.Infrastructure.Data.SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
    }
}
