namespace SuratGenerator.Core.Models;

public sealed record PathValidationResult(bool IsValid, string ErrorMessage, string? NormalizedPath)
{
    public static PathValidationResult Success(string normalizedPath) =>
        new(true, string.Empty, normalizedPath);

    public static PathValidationResult Failure(string errorMessage) =>
        new(false, errorMessage, null);
}
