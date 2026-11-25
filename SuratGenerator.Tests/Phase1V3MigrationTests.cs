using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase1V3MigrationTests
{
    private static readonly string[] V31Tables =
    {
        "SchemaVersions", "Projects", "ProjectFields", "DocumentCategories", "MasterDocuments",
        "MasterDocumentVersions", "ProjectCategories", "ProjectDocuments", "DocumentFieldValues",
        "PlaceholderMappings", "BackupRecords"
    };

    [Fact]
    public async Task MigrasiV31MenambahTabelTanpaMenghapusDataLama()
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
            using (var store = new DraftStore(CreateOptions(databasePath)))
            {
                Assert.NotNull(store);
            }

            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                var tables = ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'table';");
                foreach (var table in V31Tables)
                {
                    Assert.Contains(table, tables);
                }

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM \"Templates\" WHERE \"Id\" = $id;";
                command.Parameters.AddWithValue("$id", templateId.ToString());
                Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
            }

            using (var store = new DraftStore(CreateOptions(databasePath)))
            {
                Assert.NotNull(store);
            }

            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM \"SchemaVersions\" WHERE \"Version\" = 'v3.1';";
                Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
                command.CommandText = "SELECT COUNT(*) FROM \"SchemaVersions\" WHERE \"Version\" = 'v3.2';";
                Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
                command.CommandText = "SELECT COUNT(*) FROM \"SchemaVersions\" WHERE \"Version\" = 'v3.3';";
                Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ProjectDocuments') WHERE name IN ('IsRequired', 'FileSizeBytes', 'LastModifiedUtc');";
                Assert.Equal(3L, Convert.ToInt64(command.ExecuteScalar()));
                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ProjectDocuments') WHERE name IN ('BaseTemplateFilePath', 'EditedOutputFilePath', 'LastEditedAt');";
                Assert.Equal(3L, Convert.ToInt64(command.ExecuteScalar()));
                command.CommandText = "SELECT COUNT(*) FROM \"SchemaVersions\" WHERE \"Version\" = 'v3.4';";
                Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CategoryChecklistItems';";
                Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task EntityV31DapatDisimpanDanDibaca()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var now = DateTimeOffset.UtcNow;

        try
        {
            using (var store = new DraftStore(CreateOptions(databasePath)))
            {
                Assert.NotNull(store);
            }

            var projectId = Guid.NewGuid();
            var projectDocumentId = Guid.NewGuid();
            var masterId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var projectCategoryId = Guid.NewGuid();

            using (var db = new SuratGeneratorDbContext(CreateOptions(databasePath)))
            {
                db.Projects.Add(new ProjectEntity
                {
                    Id = projectId,
                    ShipName = "KRI TBT - 516",
                    JobName = "Perbaikan Sistem Kelistrikan",
                    RootPath = Path.Combine(directory, "Projects", "KRI TBT 516 - Perbaikan Sistem Kelistrikan"),
                    Status = "Aktif",
                    StructureVersion = "v3.1",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                db.ProjectFields.Add(new ProjectFieldEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Key = "kapal",
                    Value = "KRI TBT - 516",
                    FieldType = "Text",
                    Source = "Profil Pekerjaan"
                });
                db.DocumentCategories.Add(new DocumentCategoryEntity
                {
                    Id = categoryId,
                    Name = "01. Dokumen Tagihan",
                    NormalizedName = "01 dokumen tagihan",
                    SortOrder = 1,
                    IsActive = true,
                    Version = "1.0",
                    UpdatedAt = now
                });
                db.MasterDocuments.Add(new MasterDocumentEntity
                {
                    Id = masterId,
                    CategoryId = categoryId,
                    DisplayName = "Faktur Pembayaran Pekas",
                    SourceFileName = "Faktur Pembayaran Pekas.docx",
                    StoredFilePath = Path.Combine(directory, "master.docx"),
                    FileType = ".docx",
                    IsRequired = true,
                    IsActive = true,
                    IsArchived = false,
                    CurrentVersionId = versionId,
                    CurrentVersionNumber = "1.0",
                    FileSizeBytes = 128,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                db.MasterDocumentVersions.Add(new MasterDocumentVersionEntity
                {
                    Id = versionId,
                    MasterDocumentId = masterId,
                    VersionNumber = "1.0",
                    StoredFilePath = Path.Combine(directory, "master.docx"),
                    FileSizeBytes = 128,
                    CreatedAt = now
                });
                db.ProjectCategories.Add(new ProjectCategoryEntity
                {
                    Id = projectCategoryId,
                    ProjectId = projectId,
                    SourceCategoryId = categoryId,
                    Name = "01. Dokumen Tagihan",
                    SortOrder = 1,
                    FolderPath = Path.Combine(directory, "Projects", "01. Dokumen Tagihan")
                });
                db.ProjectDocuments.Add(new ProjectDocumentEntity
                {
                    Id = projectDocumentId,
                    ProjectId = projectId,
                    ProjectCategoryId = projectCategoryId,
                    MasterDocumentId = masterId,
                    MasterVersionId = versionId,
                    DisplayName = "Faktur Pembayaran Pekas",
                    FilePath = Path.Combine(directory, "Projects", "faktur.docx"),
                    FileType = ".docx",
                    Source = "Salinan Master",
                    Status = "Belum Tersedia",
                    VersionSnapshot = "v3.1",
                    IsRequired = true,
                    FileSizeBytes = 128,
                    LastModifiedUtc = now,
                    ImportedAt = now,
                    UpdatedAt = now
                });
                db.DocumentFieldValues.Add(new DocumentFieldValueEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectDocumentId = projectDocumentId,
                    Key = "kapal",
                    Value = "KRI TBT - 516",
                    IsOverride = false,
                    UpdatedAt = now
                });
                db.PlaceholderMappings.Add(new PlaceholderMappingEntity
                {
                    Id = Guid.NewGuid(),
                    PlaceholderKey = "{kapal}",
                    ProfileFieldKey = "ShipName",
                    IsActive = true,
                    UpdatedAt = now
                });
                db.BackupRecords.Add(new BackupRecordEntity
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now,
                    Location = Path.Combine(directory, "Backups", "backup.zip"),
                    Kind = "Manual",
                    Status = "Lulus",
                    IsVerified = true,
                    ItemCount = 1,
                    SizeBytes = 128
                });
                await db.SaveChangesAsync();
            }

            using (var db = new SuratGeneratorDbContext(CreateOptions(databasePath)))
            {
                Assert.Equal(1, await db.Projects.CountAsync());
                Assert.Equal(1, await db.ProjectFields.CountAsync());
                Assert.Equal(1, await db.DocumentCategories.CountAsync());
                Assert.Equal(1, await db.MasterDocuments.CountAsync());
                Assert.Equal(1, await db.MasterDocumentVersions.CountAsync());
                Assert.Equal(1, await db.ProjectCategories.CountAsync());
                Assert.Equal(1, await db.ProjectDocuments.CountAsync());
                Assert.Equal(1, await db.DocumentFieldValues.CountAsync());
                Assert.Equal(1, await db.PlaceholderMappings.CountAsync());
                Assert.Equal(1, await db.BackupRecords.CountAsync());
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorV3Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static DbContextOptions<SuratGeneratorDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static List<string> ReadStrings(SqliteConnection connection, string sql)
    {
        var values = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
    }
}
