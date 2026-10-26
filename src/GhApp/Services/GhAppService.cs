using GhApp.Git;
using GhApp.GitHub;
using GhApp.Helper;
using GhApp.Settings;
using GhApp.Setup;
using GhApp.Support;

namespace GhApp.Services;

internal sealed record SetupResult(
    string Repository,
    string RemoteUrl,
    string FullName,
    BotIdentity Identity,
    int TotalRepositories,
    string Helper
);

internal sealed record GeneratedConfig(
    string Content,
    IReadOnlyList<GitHubInstallation> Installations
);

internal interface IGhAppService
{
    Task<BotIdentity> GetIdentityAsync(
        AppSettings settings,
        bool verify,
        CancellationToken cancellationToken = default
    );

    Task<InstallationToken> GetTokenAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<GitHubInstallation>> ListInstallationsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default
    );

    Task<SetupResult> SetupRepositoryAsync(
        AppSettings settings,
        string repositoryPath,
        CancellationToken cancellationToken = default
    );

    Task<GeneratedConfig> GenerateConfigAsync(
        AppSettings settings,
        string? helperName,
        CancellationToken cancellationToken = default
    );
}

internal sealed class GhAppService : IGhAppService
{
    private readonly IGitHubAppClientFactory _clientFactory;
    private readonly IRepositorySetup _repositorySetup;
    private readonly IExecutableResolver _executableResolver;

    public GhAppService(
        IGitHubAppClientFactory clientFactory,
        IRepositorySetup repositorySetup,
        IExecutableResolver executableResolver
    )
    {
        _clientFactory = clientFactory;
        _repositorySetup = repositorySetup;
        _executableResolver = executableResolver;
    }

    public async Task<BotIdentity> GetIdentityAsync(
        AppSettings settings,
        bool verify,
        CancellationToken cancellationToken = default
    )
    {
        using var client = _clientFactory.Create(settings);
        return await client.GetIdentityAsync(verify, cancellationToken);
    }

    public async Task<InstallationToken> GetTokenAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        using var client = _clientFactory.Create(settings);
        var installationId = await client.GetInstallationIdAsync(cancellationToken);
        return await client.CreateInstallationTokenAsync(installationId, cancellationToken);
    }

    public async Task<IReadOnlyList<GitHubInstallation>> ListInstallationsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        using var client = _clientFactory.Create(settings);
        return await client.ListInstallationsAsync(cancellationToken);
    }

    public async Task<SetupResult> SetupRepositoryAsync(
        AppSettings settings,
        string repositoryPath,
        CancellationToken cancellationToken = default
    )
    {
        var repository = await _repositorySetup.PrepareRepoAsync(repositoryPath, cancellationToken);
        var remoteUrl = await _repositorySetup.GetRemoteUrlAsync(repository, cancellationToken);
        var remote = RemoteUrlParser.Parse(remoteUrl, settings.Domain);

        using var client = _clientFactory.Create(settings);
        var identity = await client.GetIdentityAsync(false, cancellationToken);
        var installationId = await client.GetInstallationIdAsync(cancellationToken);
        var token = await client.CreateInstallationTokenAsync(installationId, cancellationToken);
        var access = await client.RepoInInstallationAsync(
            remote.FullName,
            token.Token,
            cancellationToken
        );
        if (!access.Found)
        {
            throw new GhAppException(
                $"repository {remote.FullName} not found in the installation ({access.TotalCount} visible)"
            );
        }

        var prefix = _executableResolver.DefaultPrefix();
        var helper = HelperCommand.Build(
            prefix,
            new HelperSpec(
                settings.AppId,
                installationId,
                settings.PrivateKeyFile,
                settings.Username,
                settings.Domain
            )
        );
        await _repositorySetup.ConfigureAsync(
            repository,
            identity,
            helper,
            settings.Domain,
            cancellationToken
        );
        return new SetupResult(
            repository,
            remoteUrl,
            remote.FullName,
            identity,
            access.TotalCount,
            helper
        );
    }

    public async Task<GeneratedConfig> GenerateConfigAsync(
        AppSettings settings,
        string? helperName,
        CancellationToken cancellationToken = default
    )
    {
        using var client = _clientFactory.Create(settings);
        var installations = await client.ListInstallationsAsync(cancellationToken);
        var prefix = string.IsNullOrWhiteSpace(helperName)
            ? _executableResolver.DefaultPrefix()
            : _executableResolver.FromHelperName(helperName);
        var options = new GhApp.Generate.GenerateOptions(
            settings.Domain,
            settings.Username,
            settings.AppId,
            settings.PrivateKeyFile,
            prefix
        );
        using var writer = new StringWriter();
        GhApp.Generate.GitConfigGenerator.Write(writer, installations, options);
        return new GeneratedConfig(writer.ToString(), installations);
    }
}
