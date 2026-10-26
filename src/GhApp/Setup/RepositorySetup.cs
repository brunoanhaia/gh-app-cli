using GhApp.Git;
using GhApp.GitHub;

namespace GhApp.Setup;

internal sealed class RepositorySetup : IRepositorySetup
{
    private readonly IGitRunner _git;

    public RepositorySetup(IGitRunner git)
    {
        _git = git;
    }

    public Task<string> PrepareRepoAsync(string path, CancellationToken cancellationToken = default)
    {
        return _git.PrepareRepoAsync(path, cancellationToken);
    }

    public Task<string> GetRemoteUrlAsync(
        string repository,
        CancellationToken cancellationToken = default
    )
    {
        return _git.GetRemoteUrlAsync(repository, "origin", cancellationToken);
    }

    public async Task ConfigureAsync(
        string repository,
        BotIdentity identity,
        string helper,
        string domain,
        CancellationToken cancellationToken = default
    )
    {
        await _git.SetConfigAsync(repository, "user.name", identity.Login, cancellationToken);
        await _git.SetConfigAsync(repository, "user.email", identity.Email, cancellationToken);

        await _git.ReplaceAllConfigAsync(repository, "credential.helper", "", cancellationToken);
        var credentialKey = $"credential.https://{domain}.helper";
        await _git.ReplaceAllConfigAsync(repository, credentialKey, "", cancellationToken);
        await _git.AddConfigAsync(repository, credentialKey, helper, cancellationToken);
    }
}
