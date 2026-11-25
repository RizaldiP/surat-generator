using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Documents.Models;
using SuratGenerator.Documents.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class FolderTemplateTests
{
    [Fact]
    public async Task FolderBaruMemunculkanSemuaTemplateDanTidakMengubahFileAsli()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            Directory.CreateDirectory(folder);
            var first = Path.Combine(folder, "surat-a.docx");
            var second = Path.Combine(folder, "surat-b.docx");
            CreateDocx(first, "{nama}", "{tanggal}");
            CreateDocx(second, "{alamat}");
            var firstHash = Hash(first);
            var secondHash = Hash(second);

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);

            var result = await service.ScanAsync();
            var templates = await store.GetAllAsync();

            Assert.Equal(2, result.Imported);
            Assert.Equal(2, templates.Count);
            Assert.All(templates, template => Assert.Equal("Ada di folder", template.SourceStatusText));
            Assert.Equal(firstHash, Hash(first));
            Assert.Equal(secondHash, Hash(second));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task PindaiUlangTidakMenduplikasiTemplate()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            Directory.CreateDirectory(folder);
            CreateDocx(Path.Combine(folder, "surat.docx"), "{nama}");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);
            await service.ScanAsync();

            var second = await service.ScanAsync();
            var templates = await store.GetAllAsync();

            Assert.Equal(0, second.Imported);
            Assert.Equal(0, second.Updated);
            Assert.Equal(1, second.Unchanged);
            Assert.Single(templates);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileBerubahMemperbaruiTemplateDanMenjagaMetadata()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "surat.docx");
            CreateDocx(path, "{nama}");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);
            var firstScan = await service.ScanAsync();
            var original = Assert.Single(await store.GetAllAsync());
            await store.SetFavoriteAsync(original.Id, true);
            await store.UpdateMetadataAsync(original.Id, "Surat Khusus", "Umum", "2.1");

            CreateDocx(path, "{alamat}", "{tanggal}");
            var secondScan = await service.ScanAsync();
            var updated = Assert.Single(await store.GetAllAsync());

            Assert.Equal(1, firstScan.Imported);
            Assert.Equal(1, secondScan.Updated);
            Assert.Equal(original.Id, updated.Id);
            Assert.True(updated.IsFavorite);
            Assert.Equal("Surat Khusus", updated.Name);
            Assert.Equal("2.1", updated.Version);
            Assert.Contains(updated.Fields, field => field.Key == "alamat");
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileYangDihapusTidakMenghapusSalinanLokal()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "surat.docx");
            CreateDocx(path, "{nama}");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);
            await service.ScanAsync();
            File.Delete(path);

            var result = await service.ScanAsync();
            var template = Assert.Single(await store.GetAllAsync());

            Assert.Equal(1, result.Missing);
            Assert.True(template.HasFile);
            Assert.Equal("Tidak ada di folder", template.SourceStatusText);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileRusakDilewatiDanTidakMenghentikanPindai()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            Directory.CreateDirectory(folder);
            CreateDocx(Path.Combine(folder, "valid.docx"), "{nama}");
            await File.WriteAllTextAsync(Path.Combine(folder, "rusak.docx"), "bukan file docx");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);

            var result = await service.ScanAsync();
            var templates = await store.GetAllAsync();

            Assert.Equal(1, result.Imported);
            Assert.Equal(1, result.Errors);
            Assert.Single(templates);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task SubfolderHanyaDipindaiSaatDiaktifkan()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            var nested = Path.Combine(folder, "Sub");
            Directory.CreateDirectory(nested);
            CreateDocx(Path.Combine(nested, "surat.docx"), "{nama}");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);
            var first = await service.ScanAsync();

            await service.SetFolderAsync(folder, true);
            var second = await service.ScanAsync();
            var templates = await store.GetAllAsync();

            Assert.Equal(0, first.Imported);
            Assert.Equal(1, second.Imported);
            Assert.Single(templates);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileYangDipindahDipetakanKembaliKeTemplateYangSama()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            Directory.CreateDirectory(folder);
            var originalPath = Path.Combine(folder, "lama.docx");
            var renamedPath = Path.Combine(folder, "baru.docx");
            CreateDocx(originalPath, "{nama}");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);
            await service.ScanAsync();
            var original = Assert.Single(await store.GetAllAsync());
            File.Move(originalPath, renamedPath);

            var result = await service.ScanAsync();
            var updated = Assert.Single(await store.GetAllAsync());

            Assert.Equal(0, result.Imported);
            Assert.Equal(1, result.Updated);
            Assert.Equal(original.Id, updated.Id);
            Assert.Equal(renamedPath, updated.SourceFilePath);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileSementaraDiabaikan()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            Directory.CreateDirectory(folder);
            CreateDocx(Path.Combine(folder, "~$surat.docx"), "{nama}");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(folder, false);

            var result = await service.ScanAsync();

            Assert.Equal(0, result.Imported);
            Assert.Equal(0, result.Errors);
            Assert.Empty(await store.GetAllAsync());
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileDocDikonversiDanDiimporTanpaMengubahFileAsli()
    {
        var directory = CreateTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "Templates");
            var convertedDirectory = Path.Combine(directory, "Converted");
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(convertedDirectory);
            var source = Path.Combine(folder, "surat.doc");
            File.WriteAllText(source, "dummy legacy doc");
            var originalHash = Hash(source);

            using var store = CreateStore(directory);
            var service = CreateService(directory, store, new FakeConverter(convertedDirectory));
            await service.SetFolderAsync(folder, false);

            var result = await service.ScanAsync();
            var template = Assert.Single(await store.GetAllAsync());

            Assert.Equal(1, result.Imported);
            Assert.Equal("surat", template.Name);
            Assert.Equal("surat.doc", template.SourceFileName);
            Assert.Equal(Path.GetFullPath(source), template.SourceFilePath);
            Assert.Contains(template.Fields, field => field.Key == "nama");
            Assert.Equal(originalHash, Hash(source));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task GantiFolderMenyembunyikanTemplateDariFolderLama()
    {
        var directory = CreateTempDirectory();
        try
        {
            var oldFolder = Path.Combine(directory, "Lama");
            var newFolder = Path.Combine(directory, "Baru");
            Directory.CreateDirectory(oldFolder);
            Directory.CreateDirectory(newFolder);
            CreateDocx(Path.Combine(oldFolder, "lama.docx"), "{lama}");
            CreateDocx(Path.Combine(newFolder, "baru.docx"), "{baru}");

            using var store = CreateStore(directory);
            var service = CreateService(directory, store);
            await service.SetFolderAsync(oldFolder, false);
            await service.ScanAsync();

            await service.SetFolderAsync(newFolder, false);
            var scan = await service.ScanAsync();
            var settings = await service.GetSettingsAsync();
            var allTemplates = await store.GetAllAsync();
            var scopedTemplates = allTemplates
                .Where(template => TemplateFolderScope.IsWithin(template, settings))
                .ToList();

            Assert.Equal(2, allTemplates.Count);
            Assert.Single(scopedTemplates);
            Assert.Contains("baru.docx", scopedTemplates[0].SourceFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                scan.Messages,
                message => message.Contains("dari folder lain tidak ditampilkan", StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private sealed class FakeConverter(string outputDirectory) : ILegacyTemplateConverter
    {
        public Task<TemplateConversionResult> ConvertToDocxAsync(
            string sourcePath,
            CancellationToken cancellationToken = default)
        {
            var output = Path.Combine(outputDirectory, $"converted-{Guid.NewGuid():N}.docx");
            CreateDocx(output, "{nama}");
            return Task.FromResult(TemplateConversionResult.Success(output));
        }
    }

    private static TemplateStore CreateStore(string directory) =>
        new(CreateOptions(Path.Combine(directory, "surat-generator.db")), Path.Combine(directory, "Stored"));

    private static TemplateFolderService CreateService(
        string directory,
        TemplateStore store,
        ILegacyTemplateConverter? converter = null) =>
        new(store, new TemplateReader(), Path.Combine(directory, "template-folder.json"), converter);

    private static DbContextOptions<SuratGeneratorDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SuratGeneratorFolderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, true);
    }

    private static void CreateDocx(string path, params string[] placeholders)
    {
        using var stream = File.Create(path);
        using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        var body = new Body();
        mainPart.Document = new Document(body);
        foreach (var placeholder in placeholders)
        {
            body.Append(new Paragraph(new Run(new Text(placeholder))));
        }

        mainPart.Document.Save();
    }

    private static string Hash(string path)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(md5.ComputeHash(stream));
    }
}
