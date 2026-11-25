using SuratGenerator.Core.Models;
using SuratGenerator.Core.Services;

namespace SuratGenerator.Tests;

public class TemplateFolderScopeTests
{
    [Fact]
    public void IsWithinPathHanyaMenerimaAnakLangsungSaatSubfolderMati()
    {
        var root = Path.Combine(Path.GetTempPath(), "SuratGeneratorScopeTests", "Template");
        var direct = Path.Combine(root, "surat.docx");
        var nested = Path.Combine(root, "Sub", "surat.docx");

        Assert.True(TemplateFolderScope.IsWithinPath(direct, root, false));
        Assert.False(TemplateFolderScope.IsWithinPath(nested, root, false));
    }

    [Fact]
    public void IsWithinPathMenerimaSubfolderSaatDiaktifkan()
    {
        var root = Path.Combine(Path.GetTempPath(), "SuratGeneratorScopeTests", "Template");
        var nested = Path.Combine(root, "Sub", "surat.docx");

        Assert.True(TemplateFolderScope.IsWithinPath(nested, root, true));
    }

    [Fact]
    public void IsWithinPathMenolakFolderDenganAwalanMirip()
    {
        var root = Path.Combine(Path.GetTempPath(), "SuratGeneratorScopeTests", "Template");
        var sibling = Path.Combine(Path.GetTempPath(), "SuratGeneratorScopeTests", "TemplateLain", "surat.docx");

        Assert.False(TemplateFolderScope.IsWithinPath(sibling, root, true));
        Assert.False(TemplateFolderScope.IsWithinPath(sibling, root, false));
    }

    [Fact]
    public void IsWithinMenolakTemplateTanpaSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "SuratGeneratorScopeTests", "Template");
        var settings = new TemplateFolderSettingsInfo(root, true, null);
        var template = new TemplateInfo(
            Guid.NewGuid(),
            "Lokal",
            "lokal.docx",
            string.Empty,
            0,
            DateTimeOffset.UtcNow,
            Array.Empty<TemplateField>());

        Assert.False(TemplateFolderScope.IsWithin(template, settings));
    }
}
