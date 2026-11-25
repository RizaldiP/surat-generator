namespace SuratGenerator.Core.Models;

public sealed record ProjectFileCollisionInfo(
    string FileName,
    string RelativePath,
    string ExistingPath);
