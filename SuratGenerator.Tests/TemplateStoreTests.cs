using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class TemplateStoreTests
{
    [Fact]
    public async Task MenyalinFileDanMenyimpanMetadataKeSqlite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var templatesDirectory = Path.Combine(directory, "Templates");
        var sourcePath = Path.Combine(directory, "surat.docx");
        var databasePath = Path.Combine(directory, "test.db");
        await File.WriteAllTextAsync(sourcePath, "dummy docx untuk uji salin");

        var options = new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        var fields = new[]
        {
            new TemplateField("nama perusahaan", "Nama Perusahaan", 1, TemplateFieldKind.Text),
            new TemplateField("tanggal surat", "Tanggal Surat", 2, TemplateFieldKind.Text)
        };

        try
        {
            using var store = new TemplateStore(options, templatesDirectory);
            var info = await store.SaveAsync(sourcePath, fields);

            Assert.True(File.Exists(info.StoredFilePath));
            Assert.Equal("surat", info.Name);
            Assert.Equal("surat.docx", info.SourceFileName);
            Assert.Equal(fields.Length, info.Fields.Count);

            using var dbContext = new SuratGeneratorDbContext(options);
            var entity = await dbContext.Templates
                .Include(template => template.Fields)
                .SingleAsync(template => template.Id == info.Id);

            Assert.Equal(2, entity.Fields.Count);
            Assert.Contains(entity.Fields, field => field.Key == "nama perusahaan");
            Assert.Contains(entity.Fields, field => field.Label == "Tanggal Surat");

            var loaded = await store.GetAsync(info.Id);
            Assert.NotNull(loaded);
            Assert.Equal(info.Name, loaded!.Name);
            Assert.Equal(fields.Length, loaded.Fields.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }
}
