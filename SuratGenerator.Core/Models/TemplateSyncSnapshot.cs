namespace SuratGenerator.Core.Models;

public sealed record TemplateSyncSnapshot(
    Guid TemplateId,
    string Name,
    string Version,
    string LocalChecksum,
    DateTimeOffset LocalModifiedAt,
    string? RemoteFileId,
    string? RemoteName,
    string? RemoteVersion,
    string? RemoteChecksum,
    DateTimeOffset? RemoteModifiedAt,
    string? LastSyncedChecksum,
    string? LastSyncedVersion,
    bool IsLocallyDeleted,
    bool LocalFileExists);
