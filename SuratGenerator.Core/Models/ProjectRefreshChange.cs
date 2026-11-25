namespace SuratGenerator.Core.Models;

public sealed record ProjectRefreshChange(
    string RelativePath,
    string Action,
    string Message);
