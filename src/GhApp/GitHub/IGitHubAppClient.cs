using GhApp.Settings;

namespace GhApp.GitHub;

internal interface IGitHubAppClient : IDisposable
{
    string ApiBase { get; }

    Task<long> GetInstallationIdAsync(CancellationToken cancellationToken = default);

    Task<InstallationToken> CreateInstallationTokenAsync(
        long installationId,
        CancellationToken cancellationToken = default
    );

    Task<BotIdentity> GetIdentityAsync(bool verify, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubInstallation>> ListInstallationsAsync(
        CancellationToken cancellationToken = default
    );

    Task<RepositoryAccess> RepoInInstallationAsync(
        string fullName,
        string token,
        CancellationToken cancellationToken = default
    );

    Task<int> GetRepositoryCountAsync(string token, CancellationToken cancellationToken = default);
}

internal interface IGitHubAppClientFactory
{
    IGitHubAppClient Create(AppSettings settings);
}

internal sealed class GitHubAppClientFactory : IGitHubAppClientFactory
{
    public IGitHubAppClient Create(AppSettings settings)
    {
        return new GitHubAppClient(settings);
    }
}
