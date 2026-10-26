namespace GhApp.Git;

internal interface IGitRunner
{
    Task<GitResult> RunAsync(
        string? repository,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    );

    Task<string> RunCheckedAsync(
        string? repository,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    );

    Task<string> PrepareRepoAsync(string path, CancellationToken cancellationToken = default);

    Task<string> GetRemoteUrlAsync(
        string repository,
        string name,
        CancellationToken cancellationToken = default
    );

    Task SetConfigAsync(
        string repository,
        string key,
        string value,
        CancellationToken cancellationToken = default
    );

    Task ReplaceAllConfigAsync(
        string repository,
        string key,
        string value,
        CancellationToken cancellationToken = default
    );

    Task AddConfigAsync(
        string repository,
        string key,
        string value,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<string>> GetAllConfigAsync(
        string repository,
        string key,
        CancellationToken cancellationToken = default
    );
}
