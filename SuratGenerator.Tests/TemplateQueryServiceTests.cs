using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Tests;

public class TemplateQueryServiceTests
{
    [Fact]
    public void MencariSubstringTanpaMemperhatikanHurufBesar()
    {
        var templates = new[]
        {
            Template("Surat Penawaran Harga", "Umum", DateTimeOffset.UtcNow.AddDays(-3)),
            Template("LAMPIRAN Penawaran", "Bisnis", DateTimeOffset.UtcNow.AddDays(-1)),
            Template("Surat Izin", "Pribadi", DateTimeOffset.UtcNow)
        };

        var results = TemplateQueryService.Query(templates, "penawaran", "Semua", TemplateSort.NameAscending);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, template => template.Name == "Surat Penawaran Harga");
        Assert.Contains(results, template => template.Name == "LAMPIRAN Penawaran");
    }

    [Fact]
    public void FilterKategoriDanUrutanBekerjaBersamaan()
    {
        var older = Template("Surat A", "Bisnis", DateTimeOffset.UtcNow.AddDays(-5));
        var newer = Template("Surat Z", "Bisnis", DateTimeOffset.UtcNow.AddDays(-1));
        var other = Template("Surat B", "Pribadi", DateTimeOffset.UtcNow);
        var templates = new[] { older, newer, other };

        var byName = TemplateQueryService.Query(templates, string.Empty, "Bisnis", TemplateSort.NameDescending);
        var byDate = TemplateQueryService.Query(templates, string.Empty, "Bisnis", TemplateSort.Newest);

        Assert.Equal(new[] { "Surat Z", "Surat A" }, byName.Select(template => template.Name));
        Assert.Equal(new[] { "Surat Z", "Surat A" }, byDate.Select(template => template.Name));
        Assert.DoesNotContain(byName, template => template.Name == "Surat B");
    }

    [Fact]
    public void MencariBerdasarkanNamaFileDanKategori()
    {
        var byFile = new TemplateInfo(
            Guid.NewGuid(),
            "Surat A",
            "lampiran-khusus.docx",
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx"),
            1024,
            DateTimeOffset.UtcNow.AddDays(-2),
            Array.Empty<TemplateField>(),
            "Umum");
        var byCategory = new TemplateInfo(
            Guid.NewGuid(),
            "Surat B",
            "surat-b.docx",
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx"),
            1024,
            DateTimeOffset.UtcNow.AddDays(-1),
            Array.Empty<TemplateField>(),
            "Rahasia");
        var other = Template("Surat C", "Pribadi", DateTimeOffset.UtcNow);
        var templates = new[] { byFile, byCategory, other };

        var resultsByFile = TemplateQueryService.Query(templates, "KHUSUS", "Semua", TemplateSort.NameAscending);
        var resultsByCategory = TemplateQueryService.Query(templates, "rahasia", "Semua", TemplateSort.NameAscending);

        Assert.Equal(new[] { "Surat A" }, resultsByFile.Select(template => template.Name));
        Assert.Equal(new[] { "Surat B" }, resultsByCategory.Select(template => template.Name));
    }

    [Fact]
    public void FilterFavoritHanyaMengembalikanFavoritDanBisaDigabungDenganFilterLain()
    {
        var favoriteBisnis = Template("Surat A", "Bisnis", DateTimeOffset.UtcNow.AddDays(-2)) with { IsFavorite = true };
        var regularBisnis = Template("Surat B", "Bisnis", DateTimeOffset.UtcNow.AddDays(-1));
        var favoritePribadi = Template("Surat C", "Pribadi", DateTimeOffset.UtcNow) with { IsFavorite = true };
        var templates = new[] { favoriteBisnis, regularBisnis, favoritePribadi };

        var allFavorites = TemplateQueryService.Query(
            templates,
            string.Empty,
            "Semua",
            TemplateSort.NameAscending,
            favoritesOnly: true);
        var favoriteBisnisOnly = TemplateQueryService.Query(
            templates,
            string.Empty,
            "Bisnis",
            TemplateSort.NameAscending,
            favoritesOnly: true);
        var searchedFavorite = TemplateQueryService.Query(
            templates,
            "pribadi",
            "Semua",
            TemplateSort.NameAscending,
            favoritesOnly: true);

        Assert.Equal(new[] { "Surat A", "Surat C" }, allFavorites.Select(template => template.Name));
        Assert.Equal(new[] { "Surat A" }, favoriteBisnisOnly.Select(template => template.Name));
        Assert.Equal(new[] { "Surat C" }, searchedFavorite.Select(template => template.Name));
    }

    private static TemplateInfo Template(string name, string category, DateTimeOffset importedAt)
    {
        return new TemplateInfo(
            Guid.NewGuid(),
            name,
            $"{name}.docx",
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx"),
            1024,
            importedAt,
            Array.Empty<TemplateField>(),
            category);
    }
}
