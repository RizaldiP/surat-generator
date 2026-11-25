namespace SuratGenerator.Infrastructure.Data;

public sealed class TemplateSyncStateEntity
{
    public Guid TemplateId { get; set; }

    public string? RemoteFileId { get; set; }

    public string RemoteName { get; set; } = string.Empty;

    public string RemoteVersion { get; set; } = "1.0";

    public string? RemoteChecksum { get; set; }

    public string? LocalChecksum { get; set; }

    public DateTimeOffset? RemoteModifiedAt { get; set; }

    public DateTimeOffset? LocalModifiedAt { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public string? LastSyncedVersion { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsLocallyDeleted { get; set; }

    public string? ErrorMessage { get; set; }
}
