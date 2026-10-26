using GhApp.GitHub;

namespace GhApp.Setup;

internal interface IRepositorySetup
{
    Task<string> PrepareRepoAsync(string path, CancellationToken cancellationToken = default);

    Task<string> GetRemoteUrlAsync(
        string repository,
        CancellationToken cancellationToken = default
    );

    Task ConfigureAsync(
        string repository,
        BotIdentity identity,
        string helper,
        string domain,
        CancellationToken cancellationToken = default
    );
}
