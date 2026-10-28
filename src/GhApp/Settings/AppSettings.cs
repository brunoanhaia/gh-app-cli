using GhApp.Support;

namespace GhApp.Settings;

internal sealed class AppSettings
{
    public long AppId { get; set; }
    public long InstallationId { get; set; }
    public string Organization { get; set; } = string.Empty;
    public string PrivateKeyFile { get; set; } = string.Empty;
    public byte[] PrivateKeyPem { get; set; } = [];
    public string Username { get; set; } = "x-access-token";
    public string Domain { get; set; } = "github.com";
    public string? ConfigPath { get; set; }

    public void ValidateAuth()
    {
        if (AppId <= 0)
        {
            throw new GhAppException("config.json: fill in \"appId\" (or use -appId)");
        }
        if (string.IsNullOrWhiteSpace(PrivateKeyFile))
        {
            throw new GhAppException(
                "config.json: fill in \"privateKeyPath\" (or use -privateKeyFile)"
            );
        }
        if (PrivateKeyFile.Contains('\r') || PrivateKeyFile.Contains('\n'))
        {
            throw new GhAppException("private key path contains a newline");
        }
        if (Username.Contains('\r') || Username.Contains('\n'))
        {
            throw new GhAppException("username contains a newline");
        }

        try
        {
            PrivateKeyPem = File.ReadAllBytes(PrivateKeyFile);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            throw new GhAppException(
                $"could not read private key at {PrivateKeyFile}: {ex.Message}",
                ex
            );
        }

        if (PrivateKeyPem.Length == 0)
        {
            throw new GhAppException($"empty private key at {PrivateKeyFile}");
        }
    }

    public void ValidateInstallation()
    {
        if (InstallationId <= 0 && string.IsNullOrWhiteSpace(Organization))
        {
            throw new GhAppException("provide installationId or organization");
        }
    }
}
