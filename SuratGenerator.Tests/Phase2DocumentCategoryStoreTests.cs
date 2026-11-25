using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase2DocumentCategoryStoreTests
{
    [Fact]
    public async Task MenambahDanMengubahKategoriDenganValidasi()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);

        try
        {
            using var store = new DocumentCategoryStore(options);
            var added = await store.AddAsync("Kategori Baru");

            Assert.Equal("Kategori Baru", added.Name);
            Assert.True(added.IsActive);
            Assert.Equal(10, added.SortOrder);
            Assert.Equal("1.0", added.Version);

            await Assert.ThrowsAsync<ArgumentException>(() => store.AddAsync("kategori baru"));

            var renamed = await store.RenameAsync(added.Id, "Kategori Diubah");
            Assert.Equal(added.Id, renamed.Id);
            Assert.Equal("Kategori Diubah", renamed.Name);
            Assert.NotEqual(added.Version, renamed.Version);

            foreach (var invalidName in new[] { " ", "Nama/Invalid", "CON", "Nama." })
            {
                var name = invalidName;
                await Assert.ThrowsAsync<ArgumentException>(() => store.AddAsync(name));
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task ReorderMenghasilkanUrutanKontinuDanMenolakUrutanTidakLengkap()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);

        try
        {
            using var store = new DocumentCategoryStore(options);
            var original = await store.GetAllAsync();
            var ids = original.Select(category => category.Id).ToArray();
            var reordered = new[]
            {
                ids[2], ids[0], ids[1], ids[3], ids[4], ids[5], ids[6], ids[7], ids[8]
            };

            await store.ReorderAsync(reordered);
            var after = await store.GetAllAsync();

            Assert.Equal(
                new[] { original[2].Name, original[0].Name, original[1].Name },
                after.Take(3).Select(category => category.Name));
            Assert.Equal(Enumerable.Range(1, 9), after.Select(category => category.SortOrder));
            Assert.Equal(9, after.Select(category => category.Id).Distinct().Count());

            await Assert.ThrowsAsync<ArgumentException>(() => store.ReorderAsync(reordered.Take(2).ToArray()));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task NonaktifkanKategoriTidakMenghapusBarisDanSnapshotBerubah()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);

        try
        {
            using var store = new DocumentCategoryStore(options);
            var before = await store.GetSnapshotAsync();
            var first = (await store.GetAllAsync())[0];

            var updated = await store.SetActiveAsync(first.Id, false);
            Assert.False(updated.IsActive);
            Assert.NotEqual(first.Version, updated.Version);

            Assert.Equal(9, (await store.GetAllAsync()).Count);

            var after = await store.GetSnapshotAsync();
            Assert.NotEqual(before.Version, after.Version);
            Assert.Equal(8, after.Categories.Count);
            Assert.All(after.Categories, category => Assert.NotEqual(first.Id, category.Id));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task SnapshotDeterministikUntukStrukturYangSama()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);

        try
        {
            string firstVersion;
            List<Guid> firstIds;
            using (var store = new DocumentCategoryStore(options))
            {
                var snapshot = await store.GetSnapshotAsync();
                firstVersion = snapshot.Version;
                firstIds = snapshot.Categories.Select(category => category.Id).ToList();
            }

            using (var store = new DocumentCategoryStore(options))
            {
                var snapshot = await store.GetSnapshotAsync();
                Assert.Equal(firstVersion, snapshot.Version);
                Assert.Equal(firstIds, snapshot.Categories.Select(category => category.Id).ToList());
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task DeleteAsyncMenghapusKategoriTanpaMengubahProyekDanMasterDokumenLama()
    {
        var directory = CreateDirectory();
        var databasePath = Path.Combine(directory, "test.db");
        var options = CreateOptions(databasePath);

        try
        {
            Guid categoryId;
            using (var store = new DocumentCategoryStore(options))
            {
                categoryId = (await store.GetAllAsync()).First(category => category.Name == "SPMK").Id;
            }

            using (var db = new SuratGeneratorDbContext(options))
            {
                db.CategoryChecklistItems.Add(new CategoryChecklistItemEntity
                {
                    Id = Guid.NewGuid(),
                    CategoryId = categoryId,
                    DisplayName = "Item SPMK",
                    SortOrder = 1,
                    IsActive = true,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                db.ProjectCategories.Add(new ProjectCategoryEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = Guid.NewGuid(),
                    SourceCategoryId = categoryId,
                    Name = "SPMK",
                    SortOrder = 1,
                    FolderPath = "01 SPMK"
                });
                db.MasterDocuments.Add(new MasterDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    CategoryId = categoryId,
                    DisplayName = "Master SPMK",
                    SourceFileName = "spmk.docx",
                    StoredFilePath = "masters\\spmk.docx",
                    FileType = "docx",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            using (var store = new DocumentCategoryStore(options))
            {
                await store.DeleteAsync(categoryId);
                var remaining = await store.GetAllAsync();

                Assert.DoesNotContain(remaining, category => category.Id == categoryId);
                Assert.Equal(8, remaining.Count);
                Assert.Equal(Enumerable.Range(1, 8), remaining.Select(category => category.SortOrder));
                Assert.DoesNotContain((await store.GetSnapshotAsync()).Categories, category => category.Id == categoryId);
            }

            using (var db = new SuratGeneratorDbContext(options))
            {
                Assert.Empty(db.CategoryChecklistItems.Where(item => item.CategoryId == categoryId));
                Assert.All(
                    db.ProjectCategories.Where(category => category.SourceCategoryId == categoryId),
                    category => Assert.Null(category.SourceCategoryId));
                Assert.Equal(1, db.MasterDocuments.Count(document => document.CategoryId == categoryId));
            }

            using (var store = new DocumentCategoryStore(options))
            {
                Assert.DoesNotContain(await store.GetAllAsync(), category => category.Id == categoryId);
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
