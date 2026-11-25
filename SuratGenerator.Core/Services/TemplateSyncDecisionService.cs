using SuratGenerator.Core.Models;

namespace SuratGenerator.Core.Services;

public sealed class TemplateSyncDecisionService : ITemplateSyncDecisionService
{
    public TemplateSyncDecision Decide(TemplateSyncSnapshot snapshot)
    {
        if (snapshot.IsLocallyDeleted)
        {
            return TemplateSyncDecision.Ignore;
        }

        if (snapshot.RemoteFileId is null)
        {
            return snapshot.LocalFileExists ? TemplateSyncDecision.Upload : TemplateSyncDecision.Skip;
        }

        if (!snapshot.LocalFileExists)
        {
            return TemplateSyncDecision.Download;
        }

        if (snapshot.LastSyncedChecksum is null)
        {
            return Same(snapshot.RemoteChecksum, snapshot.LocalChecksum)
                ? TemplateSyncDecision.Skip
                : TemplateSyncDecision.Conflict;
        }

        var remoteChanged =
            !Same(snapshot.RemoteChecksum, snapshot.LastSyncedChecksum) ||
            !Same(snapshot.RemoteVersion, snapshot.LastSyncedVersion);
        var localChanged =
            !Same(snapshot.LocalChecksum, snapshot.LastSyncedChecksum) ||
            !Same(snapshot.Version, snapshot.LastSyncedVersion);

        if (remoteChanged && localChanged)
        {
            return TemplateSyncDecision.Conflict;
        }

        if (remoteChanged)
        {
            return TemplateSyncDecision.Download;
        }

        if (localChanged)
        {
            return TemplateSyncDecision.Upload;
        }

        return TemplateSyncDecision.Skip;
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
