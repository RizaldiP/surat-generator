using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SuratGenerator.Documents.Services;

namespace SuratGenerator.Tests;

public class TemplateReaderTests
{
    [Fact]
    public async Task MendeteksiPlaceholderDariParagrafTabelHeaderDanFooter()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateDocx(directory);
            var reader = new TemplateReader();

            var result = await reader.ReadAsync(path);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(
                new[] { "nama perusahaan", "nama pekerjaan", "tanggal surat", "nomor surat", "alamat perusahaan" },
                result.Fields.Select(field => field.Key));
            Assert.Equal("Nama Perusahaan", result.Fields[0].Label);
            Assert.All(result.Fields, field => Assert.Equal(SuratGenerator.Core.Models.TemplateFieldKind.Text, field.Kind));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task PlaceholderSamaHanyaMembuatSatuField()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = CreateDocx(directory);
            var result = await new TemplateReader().ReadAsync(path);

            Assert.Single(result.Fields, field =>
                string.Equals(field.Key, "nama perusahaan", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task FileRusakDanFormatSalahDitolak()
    {
        var directory = CreateTempDirectory();
        try
        {
            var broken = Path.Combine(directory, "rusak.docx");
            await File.WriteAllTextAsync(broken, "bukan dokumen word");

            var reader = new TemplateReader();
            var brokenResult = await reader.ReadAsync(broken);
            Assert.False(brokenResult.IsSuccess);
            Assert.Contains("rusak", brokenResult.Error, StringComparison.OrdinalIgnoreCase);

            var wrongExtension = Path.Combine(directory, "template.txt");
            await File.WriteAllTextAsync(wrongExtension, "placeholder");
            var extensionResult = await reader.ReadAsync(wrongExtension);
            Assert.False(extensionResult.IsSuccess);
            Assert.Contains(".docx", extensionResult.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SuratGeneratorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateDocx(string directory)
    {
        var path = Path.Combine(directory, "sample.docx");
        using var stream = File.Create(path);
        using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        var body = new Body();
        mainPart.Document = new Document(body);

        body.Append(new Paragraph(new Run(new Text("Surat untuk {nama perusahaan}"))));
        body.Append(new Paragraph(new Run(new Text("{nama")), new Run(new Text(" pekerjaan}"))));
        body.Append(new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("{tanggal surat}")))))));
        body.Append(new Paragraph(new Run(new Text("{NAMA PERUSAHAAN}"))));

        var headerPart = mainPart.AddNewPart<HeaderPart>();
        headerPart.Header = new Header(new Paragraph(new Run(new Text("{nomor surat}"))));

        var footerPart = mainPart.AddNewPart<FooterPart>();
        footerPart.Footer = new Footer(new Paragraph(new Run(new Text("{alamat perusahaan}"))));

        body.Append(new SectionProperties(
            new HeaderReference { Id = mainPart.GetIdOfPart(headerPart), Type = HeaderFooterValues.Default },
            new FooterReference { Id = mainPart.GetIdOfPart(footerPart), Type = HeaderFooterValues.Default }));

        mainPart.Document.Save();
        return path;
    }
}
