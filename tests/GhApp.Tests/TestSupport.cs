using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GhApp.Cli;
using GhApp.Credential;
using GhApp.Git;
using GhApp.GitHub;
using GhApp.Helper;
using GhApp.Services;
using GhApp.Settings;
using GhApp.Setup;

namespace GhApp.Tests;

internal static class TestKeys
{
    public static string PrivateKeyPem { get; } = CreatePem();

    public static byte[] PrivateKeyBytes => Encoding.UTF8.GetBytes(PrivateKeyPem);

    public static string WriteKeyFile(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "pk.pem");
        File.WriteAllText(path, PrivateKeyPem);
        return path;
    }

    private static string CreatePem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(_handler(request));
    }
}

internal static class HttpResponses
{
    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage Status(HttpStatusCode status, string body = "")
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
    }
}

internal sealed class FakeGitHubAppClient : IGitHubAppClient
{
    public string ApiBase => "https://api.test";

    public long InstallationId { get; set; } = 123;

    public BotIdentity Identity { get; set; } =
        new(
            "orih-agent",
            "orih-agent[bot]",
            347995100,
            "347995100+orih-agent[bot]@users.noreply.github.com"
        );

    public InstallationToken Token { get; set; } =
        new("ghs_test", DateTimeOffset.FromUnixTimeSeconds(1_900_000_000));

    public RepositoryAccess Access { get; set; } = new(true, 39);

    public IReadOnlyList<GitHubInstallation> Installations { get; set; } = [];

    public int RepositoryCount { get; set; } = 39;

    public bool Disposed { get; private set; }

    public Task<long> GetInstallationIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(InstallationId);
    }

    public Task<InstallationToken> CreateInstallationTokenAsync(
        long installationId,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(Token);
    }

    public Task<BotIdentity> GetIdentityAsync(
        bool verify,
        CancellationToken cancellationToken = default
    )
    {
        var identity = verify ? Identity with { Repositories = RepositoryCount } : Identity;
        return Task.FromResult(identity);
    }

    public Task<IReadOnlyList<GitHubInstallation>> ListInstallationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(Installations);
    }

    public Task<RepositoryAccess> RepoInInstallationAsync(
        string fullName,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(Access);
    }

    public Task<int> GetRepositoryCountAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult(RepositoryCount);
    }

    public void Dispose()
    {
        Disposed = true;
    }
}

internal sealed class FakeGitHubAppClientFactory : IGitHubAppClientFactory
{
    public FakeGitHubAppClient Client { get; } = new();

    public AppSettings? LastSettings { get; private set; }

    public IGitHubAppClient Create(AppSettings settings)
    {
        LastSettings = settings;
        return Client;
    }
}

internal sealed class FakeGitCredentialCache : IGitCredentialCache
{
    public CachedCredential? Value { get; set; }

    public bool GetCalled { get; private set; }

    public bool StoreCalled { get; private set; }

    public string? LastDomain { get; private set; }

    public string? LastKey { get; private set; }

    public string? LastUsername { get; private set; }

    public string? LastToken { get; private set; }

    public DateTimeOffset? LastExpires { get; private set; }

    public Task<CachedCredential?> GetAsync(
        string domain,
        string key,
        CancellationToken cancellationToken = default
    )
    {
        GetCalled = true;
        LastDomain = domain;
        LastKey = key;
        return Task.FromResult(Value);
    }

    public Task StoreAsync(
        string domain,
        string key,
        string username,
        string token,
        DateTimeOffset expires,
        CancellationToken cancellationToken = default
    )
    {
        StoreCalled = true;
        LastDomain = domain;
        LastKey = key;
        LastUsername = username;
        LastToken = token;
        LastExpires = expires;
        return Task.CompletedTask;
    }
}

internal sealed class FakeGhCliRunner : IGhCliRunner
{
    public int ExitCode { get; set; }

    public AppSettings? LastSettings { get; private set; }

    public string? LastToken { get; private set; }

    public IReadOnlyList<string> LastArgs { get; private set; } = [];

    public Task<int> RunAsync(
        AppSettings settings,
        string token,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default
    )
    {
        LastSettings = settings;
        LastToken = token;
        LastArgs = args;
        return Task.FromResult(ExitCode);
    }
}

internal sealed class FakeExecutableResolver : IExecutableResolver
{
    public IReadOnlyList<string> Prefix { get; set; } = ["gh-app"];

    public IReadOnlyList<string> DefaultPrefix()
    {
        return Prefix;
    }

    public IReadOnlyList<string> FromHelperName(string? helperName)
    {
        return string.IsNullOrWhiteSpace(helperName) ? Prefix : [helperName.Trim()];
    }
}

internal static class TestFiles
{
    public static string WriteConfig(
        string directory,
        string keyPath,
        long appId = 123,
        long? installationId = 456,
        string? organization = null,
        string domain = "github.com",
        string username = "x-access-token"
    )
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        var config = new Dictionary<string, object?>
        {
            ["appId"] = appId,
            ["privateKeyPath"] = keyPath,
            ["username"] = username,
            ["domain"] = domain,
        };
        if (installationId.HasValue)
        {
            config["installationId"] = installationId.Value;
        }
        if (!string.IsNullOrEmpty(organization))
        {
            config["organization"] = organization;
        }
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    public static async Task<string> CreateRepositoryAsync(string directory, string remoteUrl)
    {
        Directory.CreateDirectory(directory);
        var git = new GitRunner();
        await git.RunCheckedAsync(directory, ["init", "-q"]);
        await git.RunCheckedAsync(directory, ["remote", "add", "origin", remoteUrl]);
        return directory;
    }

    public static CommandDispatcher CreateDispatcher(
        TextWriter output,
        TextWriter error,
        TextReader input,
        bool inputRedirected,
        IGitHubAppClientFactory factory,
        IExecutableResolver? resolver = null,
        IGitCredentialCache? credentialCache = null,
        IGhCliRunner? ghCli = null
    )
    {
        var git = new GitRunner();
        var service = new GhAppService(
            factory,
            new RepositorySetup(git),
            resolver ?? new FakeExecutableResolver()
        );
        return new CommandDispatcher(
            new ConsoleContext(output, error, input, inputRedirected),
            service,
            credentialCache ?? new FakeGitCredentialCache(),
            ghCli ?? new FakeGhCliRunner()
        );
    }
}
