using SuratGenerator.Core.Models;

namespace SuratGenerator.Infrastructure.Data;

public sealed class GeneratedDocumentEntity
{
    public Guid Id { get; set; }

    public Guid TemplateId { get; set; }

    public string TemplateName { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public GeneratedDocumentKind Kind { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }
}
