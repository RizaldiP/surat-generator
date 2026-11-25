using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SuratGenerator.Core.Models;
using SuratGenerator.Documents.Models;

namespace SuratGenerator.Documents.Services;

public sealed class TemplateReader : ITemplateReader
{
    private static readonly Regex PlaceholderRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);

    public Task<TemplateReadResult> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Read(filePath, cancellationToken), cancellationToken);
    }

    private static TemplateReadResult Read(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return TemplateReadResult.Failure("Path template tidak valid.");
        }

        if (!File.Exists(filePath))
        {
            return TemplateReadResult.Failure("File template tidak ditemukan.");
        }

        if (!string.Equals(Path.GetExtension(filePath), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return TemplateReadResult.Failure("Format file tidak didukung. Gunakan file .docx.");
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0)
            {
                return TemplateReadResult.Failure("File template kosong.");
            }

            using var document = WordprocessingDocument.Open(stream, false);
            var mainPart = document.MainDocumentPart;
            if (mainPart?.Document?.Body is null)
            {
                return TemplateReadResult.Failure("Template Word tidak memiliki isi yang dapat dibaca.");
            }

            var fields = new List<TemplateField>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Collect(mainPart.Document.Body, fields, seen, cancellationToken);

            foreach (var headerPart in mainPart.HeaderParts)
            {
                if (headerPart.Header is not null)
                {
                    Collect(headerPart.Header, fields, seen, cancellationToken);
                }
            }

            foreach (var footerPart in mainPart.FooterParts)
            {
                if (footerPart.Footer is not null)
                {
                    Collect(footerPart.Footer, fields, seen, cancellationToken);
                }
            }

            if (fields.Count == 0)
            {
                return TemplateReadResult.Failure("Tidak ada placeholder ditemukan. Gunakan format {nama field}.");
            }

            return TemplateReadResult.Success(fields.ToArray());
        }
        catch (OpenXmlPackageException)
        {
            return TemplateReadResult.Failure("File Word rusak atau tidak didukung.");
        }
        catch (InvalidDataException)
        {
            return TemplateReadResult.Failure("File Word rusak atau tidak didukung.");
        }
        catch (FileFormatException)
        {
            return TemplateReadResult.Failure("File Word rusak atau tidak didukung.");
        }
        catch (IOException)
        {
            return TemplateReadResult.Failure("File template tidak dapat dibaca.");
        }
        catch (UnauthorizedAccessException)
        {
            return TemplateReadResult.Failure("Akses ke file template ditolak.");
        }
    }

    private static void Collect(
        OpenXmlElement root,
        List<TemplateField> fields,
        HashSet<string> seen,
        CancellationToken cancellationToken)
    {
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text ?? string.Empty));
            foreach (Match match in PlaceholderRegex.Matches(text))
            {
                var key = match.Groups[1].Value.Trim();
                if (key.Length == 0 || !seen.Add(key))
                {
                    continue;
                }

                fields.Add(new TemplateField(
                    key,
                    FormatLabel(key),
                    fields.Count + 1,
                    TemplateFieldKind.Text));
            }
        }
    }

    private static string FormatLabel(string key)
    {
        var words = key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return key;
        }

        return string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
