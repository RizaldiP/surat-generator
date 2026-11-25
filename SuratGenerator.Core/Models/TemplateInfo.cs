using System.IO;

namespace SuratGenerator.Core.Models;

public sealed record TemplateInfo(
    Guid Id,
    string Name,
    string SourceFileName,
    string StoredFilePath,
    long FileSizeBytes,
    DateTimeOffset ImportedAt,
    IReadOnlyList<TemplateField> Fields,
    string Category = "Umum",
    bool IsFavorite = false,
    string Version = "1.0",
    string? SourceFilePath = null,
    string? SourceChecksum = null,
    DateTimeOffset? SourceModifiedAt = null)
{
    public string ImportedAtText => ImportedAt.ToLocalTime().ToString("dd MMM yyyy HH:mm");

    public string FileSizeText => $"{FileSizeBytes / 1024.0:F0} KB";

    public string FieldCountText => $"{Fields.Count} field";

    public bool HasFile => File.Exists(StoredFilePath);

    public string FileStatusText => HasFile ? "Tersedia" : "File hilang";

    public bool HasSource => !string.IsNullOrWhiteSpace(SourceFilePath);

    public bool SourceExists => HasSource && File.Exists(SourceFilePath);

    public string SourceStatusText => !HasSource
        ? "Lokal"
        : SourceExists ? "Ada di folder" : "Tidak ada di folder";

    public string FavoriteText => IsFavorite ? "Ya" : "Tidak";
}
