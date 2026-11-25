using Google.Apis.Auth.OAuth2;

namespace SuratGenerator.Infrastructure.Services;

internal static class GoogleOAuthClientLoader
{
    public static bool HasClientFile => File.Exists(AppPaths.OAuthClientPath);

    public static ClientSecrets Load()
    {
        if (!HasClientFile)
        {
            throw new InvalidOperationException(
                "File konfigurasi Google OAuth belum tersedia. Letakkan oauth-client.json di folder data aplikasi.");
        }

        try
        {
            var secrets = GoogleClientSecrets.FromFile(AppPaths.OAuthClientPath).Secrets;
            if (string.IsNullOrWhiteSpace(secrets.ClientId) || string.IsNullOrWhiteSpace(secrets.ClientSecret))
            {
                throw new InvalidOperationException("Client ID atau Client Secret kosong.");
            }

            return secrets;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("File konfigurasi Google OAuth tidak valid.", ex);
        }
    }
}
