using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase2MasterStructureSeedTests
{
    private static readonly string[] DefaultNames =
    {
        "Dokumen Tagihan", "Efaktur", "Undangan Verifikasi", "SPMK", "JAMLAK",
        "JAMHAR", "Pengumuman Tender", "Penunjukan Pemenang", "Format Verifikasi"
    };

    [Fact]
    public async Task SeedMembuatSembilanKategoriTanpaPrefixDanIdempoten()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);

        try
        {
            using (var store = new DocumentCategoryStore(options))
            {
                var categories = await store.GetAllAsync();

                Assert.Equal(9, categories.Count);
                Assert.Equal(DefaultNames, categories.Select(category => category.Name));
                Assert.Equal(Enumerable.Range(1, 9), categories.Select(category => category.SortOrder));
                Assert.All(categories, category =>
                {
                    Assert.True(category.IsActive);
                    Assert.Equal("1.0", category.Version);
                    Assert.False(char.IsDigit(category.Name[0]));
                });
                Assert.Equal(9, categories.Select(category => category.Id).Distinct().Count());
            }

            using (var store = new DocumentCategoryStore(options))
            {
                Assert.Equal(9, (await store.GetAllAsync()).Count);
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"SchemaVersions\" WHERE \"Version\" = 'seed.document-categories.v1';";
            Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task SeedTidakMengubahKategoriYangSudahAda()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);
        var existingId = Guid.NewGuid();

        try
        {
            using (var initializer = new DraftStore(options))
            {
                Assert.NotNull(initializer);
            }

            using (var db = new SuratGeneratorDbContext(options))
            {
                db.DocumentCategories.Add(new DocumentCategoryEntity
                {
                    Id = existingId,
                    Name = "Kategori Existing",
                    NormalizedName = "KATEGORI EXISTING",
                    SortOrder = 1,
                    IsActive = true,
                    Version = "1.0",
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            using (var store = new DocumentCategoryStore(options))
            {
                var category = Assert.Single(await store.GetAllAsync());
                Assert.Equal(existingId, category.Id);
                Assert.Equal("Kategori Existing", category.Name);
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"SchemaVersions\" WHERE \"Version\" = 'seed.document-categories.v1';";
            Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task SeedTidakMengulangJikaMarkerAdaDanTabelKosong()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);

        try
        {
            using (var store = new DocumentCategoryStore(options))
            {
                Assert.Equal(9, (await store.GetAllAsync()).Count);
            }

            using (var db = new SuratGeneratorDbContext(options))
            {
                db.DocumentCategories.RemoveRange(await db.DocumentCategories.ToListAsync());
                await db.SaveChangesAsync();
            }

            using (var store = new DocumentCategoryStore(options))
            {
                Assert.Empty(await store.GetAllAsync());
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase2Tests", Guid.NewGuid().ToString("N"));
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
