using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;
using SuratGenerator.Documents.Services;
using SuratGenerator.Infrastructure.Data;
using SuratGenerator.Infrastructure.Services;

namespace SuratGenerator.Tests;

public class Phase7Tests
{
    [Fact]
    public void FileLoggerMenulisLogDanMenyembunyikanRahasia()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "logs", "app.log");
            using var logger = new FileLogger(path);
            logger.Error("Gagal OAuth client_secret=rahasia-abc123", new InvalidOperationException("token=rahasia-xyz789"));

            var content = File.ReadAllText(path);
            Assert.Contains("[ERROR]", content);
            Assert.Contains("[REDACTED]", content);
            Assert.DoesNotContain("rahasia-abc123", content);
            Assert.DoesNotContain("rahasia-xyz789", content);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void FileLoggerMemutarLogBesar()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "app.log");
            using (var logger = new FileLogger(path))
            {
                logger.Error(new string('x', 2 * 1024 * 1024 + 128));
            }

            Assert.True(File.Exists(path + ".1"), "File log lama harus diputar.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task MigrasiSqliteMenjagaDataDanMenambahOptimisasi()
    {
        var directory = CreateTempDirectory();
        try
        {
            var databasePath = Path.Combine(directory, "surat-generator.db");
            var templatesDirectory = Path.Combine(directory, "Templates");
            var samplePath = CreateDocx(directory);
            var options = CreateOptions(databasePath);
            var templateId = Guid.Empty;

            using (var store = new TemplateStore(options, templatesDirectory))
            {
                var template = await store.SaveAsync(samplePath, Array.Empty<TemplateField>());
                templateId = template.Id;
            }

            using (var store = new TemplateStore(options, templatesDirectory))
            {
                var templates = await store.GetAllAsync();
                Assert.Contains(templates, template => template.Id == templateId);
            }

            using var context = new SuratGeneratorDbContext(options);
            var journalMode = (await context.Database
                    .SqlQueryRaw<string>("PRAGMA journal_mode")
                    .ToListAsync())
                .Single();
            Assert.Equal("wal", journalMode, ignoreCase: true);

            var indexes = await context.Database
                .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type = 'index' AND name LIKE 'IX_%' ORDER BY name")
                .ToListAsync();
            Assert.Contains("IX_Drafts_TemplateId", indexes);
            Assert.Contains("IX_GeneratedDocuments_GeneratedAt", indexes);
            Assert.Contains("IX_DriveSyncStates_Status", indexes);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task GoogleDriveServiceMenolakSinkronisasiSaatOffline()
    {
        var directory = CreateTempDirectory();
        try
        {
            var options = CreateOptions(Path.Combine(directory, "surat-generator.db"));
            using var store = new TemplateStore(options, Path.Combine(directory, "Templates"));
            using var service = new GoogleDriveService(
                store,
                new TemplateReader(),
                new TemplateSyncDecisionService(),
                options,
                new OfflineNetworkStatusProvider(),
                NullAppLogger.Instance);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ValidateFolderAsync("https://drive.google.com/drive/folders/abcdefghijklmnopqrstuvwxyz"));

            Assert.Contains("Tidak ada koneksi internet", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task PlaceholderPadaDokumenBanyakBagianTerdeteksi()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateMultiSectionDocx(directory);
            var result = await new TemplateReader().ReadAsync(path);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Contains(result.Fields, field => field.Key == "halaman pertama");
            Assert.Contains(result.Fields, field => field.Key == "halaman kedua");
            Assert.Contains(result.Fields, field => field.Key == "kop surat");
            Assert.Contains(result.Fields, field => field.Key == "kaki surat");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static DbContextOptions<SuratGeneratorDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<SuratGeneratorDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SuratGeneratorPhase7", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateDocx(string directory)
    {
        var path = Path.Combine(directory, "sample.docx");
        using var stream = File.Create(path);
        using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("{nama perusahaan}")))));
        mainPart.Document.Save();
        return path;
    }

    private static string CreateMultiSectionDocx(string directory)
    {
        var path = Path.Combine(directory, "multi-section.docx");
        using var stream = File.Create(path);
        using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        var body = new Body();
        mainPart.Document = new Document(body);

        body.Append(new Paragraph(new Run(new Text("{halaman pertama}"))));
        body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
        body.Append(new Paragraph(new Run(new Text("{halaman kedua}"))));

        var headerPart = mainPart.AddNewPart<HeaderPart>();
        headerPart.Header = new Header(new Paragraph(new Run(new Text("{kop surat}"))));
        var footerPart = mainPart.AddNewPart<FooterPart>();
        footerPart.Footer = new Footer(new Paragraph(new Run(new Text("{kaki surat}"))));
        body.Append(new SectionProperties(
            new HeaderReference { Id = mainPart.GetIdOfPart(headerPart), Type = HeaderFooterValues.Default },
            new FooterReference { Id = mainPart.GetIdOfPart(footerPart), Type = HeaderFooterValues.Default }));

        mainPart.Document.Save();
        return path;
    }

    private sealed class OfflineNetworkStatusProvider : INetworkStatusProvider
    {
        public bool IsOnline => false;
    }
}
