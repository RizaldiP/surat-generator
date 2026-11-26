using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class NoteStoreTests
{
    [Fact]
    public async Task CatatanStoreMenyimpanMengubahMengurutkanDanMenghapus()
    {
        var environment = CreateEnvironment();
        try
        {
            var first = await environment.Notes.AddAsync("Pembuatan SIK");
            var second = await environment.Notes.AddAsync("Pembuatan SK");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                environment.Notes.AddAsync("pembuatan sik"));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                environment.Notes.AddAsync("   "));

            var renamed = await environment.Notes.RenameAsync(first.Id, "Pembuatan SIK Baru");
            Assert.Equal("Pembuatan SIK Baru", renamed.Title);

            var items = await environment.Items.AddAsync(first.Id, "Surat Permohonan", "Ditandatangani kepala bidang.");
            Assert.Empty(await environment.Items.GetByNoteAsync(second.Id));
            Assert.Equal("Ditandatangani kepala bidang.", items.Description);

            var all = await environment.Notes.GetAllAsync();
            Assert.Equal(2, all.Count);
            Assert.Equal("Pembuatan SIK Baru", all[0].Title);

            await environment.Notes.ReorderAsync(new[] { second.Id, first.Id });
            var reordered = await environment.Notes.GetAllAsync();
            Assert.Equal("Pembuatan SK", reordered[0].Title);
            Assert.Equal(1, reordered[0].SortOrder);
            Assert.Equal(2, reordered[1].SortOrder);

            await environment.Notes.DeleteAsync(first.Id);
            Assert.Single(await environment.Notes.GetAllAsync());
            Assert.Empty(await environment.Items.GetByNoteAsync(first.Id));

            var remaining = await environment.Notes.GetAllAsync();
            Assert.Equal(1, remaining[0].SortOrder);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    [Fact]
    public async Task ItemCatatanMenyimpanKeteranganDanMenolakDuplikat()
    {
        var environment = CreateEnvironment();
        try
        {
            var note = await environment.Notes.AddAsync("Pembuatan SIK");
            var first = await environment.Items.AddAsync(note.Id, "Surat Permohonan", "Dari pemohon");
            var second = await environment.Items.AddAsync(note.Id, "SC", string.Empty);
            await environment.Items.AddAsync(note.Id, "SPMK", "Baru berlaku setelah tanda tangan");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                environment.Items.AddAsync(note.Id, "sc", "duplikat"));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                environment.Items.AddAsync(note.Id, "   ", string.Empty));

            var updated = await environment.Items.UpdateAsync(first.Id, "Surat Permohonan", "Dari bagian umum\nDua lampiran");
            Assert.Equal("Dari bagian umum\nDua lampiran", updated.Description);

            await environment.Items.ReorderAsync(
                note.Id,
                new[] { second.Id, first.Id, (await environment.Items.GetByNoteAsync(note.Id)).Last().Id });
            var items = await environment.Items.GetByNoteAsync(note.Id);
            Assert.Equal("SC", items[0].DisplayName);
            Assert.Equal(1, items[0].SortOrder);

            await environment.Items.DeleteAsync(second.Id);
            var afterDelete = await environment.Items.GetByNoteAsync(note.Id);
            Assert.Equal(2, afterDelete.Count);
            Assert.DoesNotContain(afterDelete, item => item.Id == second.Id);
        }
        finally
        {
            Cleanup(environment);
        }
    }

    private static TestEnvironment CreateEnvironment()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SuratGeneratorNoteTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var options = new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={Path.Combine(directory, "test.db")}")
            .Options;
        return new TestEnvironment(
            directory,
            options,
            new NoteStore(options),
            new NoteItemStore(options));
    }

    private static void Cleanup(TestEnvironment environment)
    {
        environment.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(environment.Directory))
        {
            Directory.Delete(environment.Directory, true);
        }
    }

    private sealed class TestEnvironment : IDisposable
    {
        public TestEnvironment(
            string directory,
            DbContextOptions<SuratGeneratorDbContext> options,
            NoteStore notes,
            NoteItemStore items)
        {
            Directory = directory;
            Options = options;
            Notes = notes;
            Items = items;
        }

        public string Directory { get; }

        public DbContextOptions<SuratGeneratorDbContext> Options { get; }

        public NoteStore Notes { get; }

        public NoteItemStore Items { get; }

        public void Dispose()
        {
            Notes.Dispose();
            Items.Dispose();
        }
    }
}
