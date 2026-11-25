namespace SuratGenerator.Core.Models;

public sealed record WorkspaceFileMetadata(
    string RelativePath,
    string FileName,
    long Length,
    DateTimeOffset LastWriteTimeUtc);
