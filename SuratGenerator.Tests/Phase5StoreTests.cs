using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase5StoreTests
{
    [Fact]
    public async Task MenambahKolomMetadataKeDatabaseLama()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var templateId = Guid.NewGuid();

        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            Execute(connection, """
                CREATE TABLE "Templates" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Templates" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "SourceFileName" TEXT NOT NULL,
                    "StoredFilePath" TEXT NOT NULL,
                    "FileSizeBytes" INTEGER NOT NULL,
                    "ImportedAt" TEXT NOT NULL
                );
                """);
            Execute(connection, """
                CREATE TABLE "TemplateFields" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_TemplateFields" PRIMARY KEY AUTOINCREMENT,
                    "TemplateId" TEXT NOT NULL,
                    "Key" TEXT NOT NULL,
                    "Label" TEXT NOT NULL,
                    "Position" INTEGER NOT NULL,
                    "Kind" TEXT NOT NULL,
                    CONSTRAINT "FK_TemplateFields_Templates_TemplateId"
                        FOREIGN KEY ("TemplateId") REFERENCES "Templates" ("Id") ON DELETE CASCADE
                );
                """);
            Execute(connection, "CREATE INDEX \"IX_TemplateFields_TemplateId\" ON \"TemplateFields\" (\"TemplateId\");");
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO "Templates" ("Id", "Name", "SourceFileName", "StoredFilePath", "FileSizeBytes", "ImportedAt")
                VALUES ($id, 'Template Lama', 'lama.docx', 'lama.docx', 1024, '2026-01-01T00:00:00+00:00');
                """;
            command.Parameters.AddWithValue("$id", templateId.ToString());
            command.ExecuteNonQuery();
        }

        try
        {
            using (var store = new TemplateStore(CreateOptions(databasePath), Path.Combine(directory, "Templates")))
            {
                var template = Assert.Single(await store.GetAllAsync());
                Assert.Equal("Umum", template.Category);
                Assert.False(template.IsFavorite);
                Assert.Equal("1.0", template.Version);
            }

            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Templates') WHERE name IN ('Category', 'IsFavorite', 'Version');";
                Assert.Equal(3L, Convert.ToInt64(command.ExecuteScalar()));
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task MetadataDapatDiubahDanHapusTemplateMenghapusDraft()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var templatesDirectory = Path.Combine(directory, "Templates");
        var sourcePath = Path.Combine(directory, "surat.docx");
        await File.WriteAllTextAsync(sourcePath, "dummy docx");

        try
        {
            TemplateInfo template;
            using (var store = new TemplateStore(CreateOptions(databasePath), templatesDirectory))
            {
                template = await store.SaveAsync(
                    sourcePath,
                    new[] { new TemplateField("nama", "Nama", 1, TemplateFieldKind.Text) });
                await store.UpdateMetadataAsync(template.Id, "Penawaran Baru", "Bisnis", "2.1");
                await store.SetFavoriteAsync(template.Id, true);
            }

            using (var draftStore = new DraftStore(CreateOptions(databasePath)))
            {
                await draftStore.SaveAsync(new DraftInfo(
                    Guid.NewGuid(),
                    template.Id,
                    template.Name,
                    "Draft Terkait",
                    new Dictionary<string, string> { ["nama"] = "PT Uji" },
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow));
            }

            using (var store = new TemplateStore(CreateOptions(databasePath), templatesDirectory))
            {
                var all = await store.GetAllAsync();
                var loaded = Assert.Single(all);
                Assert.Equal("Penawaran Baru", loaded.Name);
                Assert.Equal("Bisnis", loaded.Category);
                Assert.Equal("2.1", loaded.Version);
                Assert.True(loaded.IsFavorite);
            }

            using (var store = new TemplateStore(CreateOptions(databasePath), templatesDirectory))
            {
                Assert.True(await store.DeleteAsync(template.Id));
            }

            using (var store = new TemplateStore(CreateOptions(databasePath), templatesDirectory))
            {
                Assert.Null(await store.GetAsync(template.Id));
            }

            using (var draftStore = new DraftStore(CreateOptions(databasePath)))
            {
                Assert.Empty(await draftStore.GetAllAsync());
            }

            Assert.False(File.Exists(template.StoredFilePath));
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

    private static DbContextOptions<SuratGeneratorDbContext> CreateOptions(string databasePath)
    {
        return new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
    }
}
