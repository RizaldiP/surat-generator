namespace SuratGenerator.Core.Models;

public sealed record WorkspacePathsInfo(
    string MasterDocumentsDirectory,
    string ProjectsDirectory,
    string BackupsDirectory);
