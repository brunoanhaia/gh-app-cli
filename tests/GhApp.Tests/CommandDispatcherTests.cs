using System.Text;
using GhApp.Cli;
using GhApp.Git;
using GhApp.GitHub;

namespace GhApp.Tests;

public sealed class CommandDispatcherTests
{
    [Fact]
    public async Task CredentialStoreDrainsInput()
    {
        var input = new StringReader("protocol=https\nhost=github.com\n\n");
        var output = new StringWriter();
        var error = new StringWriter();
        var dispatcher = TestFiles.CreateDispatcher(
            output,
            error,
            input,
            inputRedirected: true,
            new FakeGitHubAppClientFactory()
        );

        var code = await dispatcher.RunAsync(["credential", "store"]);
        Assert.Equal(0, code);
        Assert.Equal(-1, input.Peek());
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public async Task CredentialGetHostMismatchDoesNotCallApi()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var factory = new FakeGitHubAppClientFactory();
            var dispatcher = TestFiles.CreateDispatcher(
                new StringWriter(),
                new StringWriter(),
                new StringReader("protocol=https\nhost=gitlab.com\n\n"),
                inputRedirected: true,
                factory
            );

            var code = await dispatcher.RunAsync(["credential", "get", "-config", configPath]);
            Assert.Equal(0, code);
            Assert.Null(factory.LastSettings);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task CredentialGetWritesToken()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var factory = new FakeGitHubAppClientFactory();
            factory.Client.Token = new InstallationToken(
                "ghs_test",
                DateTimeOffset.FromUnixTimeSeconds(1_900_000_000)
            );
            var output = new StringWriter();
            var error = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                output,
                error,
                new StringReader("protocol=https\nhost=github.com\n\n"),
                inputRedirected: true,
                factory
            );

            var code = await dispatcher.RunAsync(["credential", "get", "-config", configPath]);
            Assert.Equal(0, code);
            Assert.Equal(
                "username=x-access-token\npassword=ghs_test\npassword_expiry_utc=1900000000\n",
                output.ToString()
            );
            Assert.Equal("", error.ToString());
            Assert.True(factory.Client.Disposed);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task CredentialGetSupportsDirectHelperInvocation()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var factory = new FakeGitHubAppClientFactory();
            factory.Client.Token = new InstallationToken(
                "ghs_direct",
                DateTimeOffset.FromUnixTimeSeconds(1_900_000_000)
            );
            var output = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                output,
                new StringWriter(),
                new StringReader("protocol=https\nhost=github.com\n\n"),
                inputRedirected: true,
                factory
            );

            var code = await dispatcher.RunAsync(["get", "-config", configPath]);
            Assert.Equal(0, code);
            Assert.Equal(
                "username=x-access-token\npassword=ghs_direct\npassword_expiry_utc=1900000000\n",
                output.ToString()
            );
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task HelperModeIgnoresUnknownOperations()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var dispatcher = TestFiles.CreateDispatcher(
            output,
            error,
            new StringReader(""),
            inputRedirected: false,
            new FakeGitHubAppClientFactory()
        );

        var code = await dispatcher.RunAsync(["-config", "missing.json", "future-operation"]);
        Assert.Equal(0, code);
        Assert.Equal("", output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public async Task HelperModeIgnoresUnknownPipedOperation()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var dispatcher = TestFiles.CreateDispatcher(
            output,
            error,
            new StringReader(""),
            inputRedirected: true,
            new FakeGitHubAppClientFactory()
        );

        var code = await dispatcher.RunAsync(["future-operation"]);
        Assert.Equal(0, code);
        Assert.Equal("", output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public async Task CredentialGetFailureWritesQuit()
    {
        var workspace = NewWorkspace(out var _);
        try
        {
            var missingKey = Path.Combine(workspace, "missing.pem");
            var configPath = TestFiles.WriteConfig(workspace, missingKey);
            var factory = new FakeGitHubAppClientFactory();
            var output = new StringWriter();
            var error = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                output,
                error,
                new StringReader("protocol=https\nhost=github.com\n\n"),
                inputRedirected: true,
                factory
            );

            var code = await dispatcher.RunAsync(["credential", "get", "-config", configPath]);
            Assert.Equal(1, code);
            Assert.Equal("quit=1\n", output.ToString());
            Assert.Contains("could not read private key", error.ToString());
            Assert.Null(factory.LastSettings);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task IdentityWritesJsonWithPlusUnescaped()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var factory = new FakeGitHubAppClientFactory();
            var output = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                output,
                new StringWriter(),
                new StringReader(""),
                inputRedirected: false,
                factory
            );

            var code = await dispatcher.RunAsync(["identity", "-config", configPath]);
            Assert.Equal(0, code);
            var json = output.ToString();
            Assert.Contains("\"slug\":\"orih-agent\"", json);
            Assert.Contains("+orih-agent[bot]@users.noreply.github.com", json);
            Assert.DoesNotContain("repositories", json);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task IdentityVerifyIncludesRepositoryCount()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var factory = new FakeGitHubAppClientFactory();
            factory.Client.RepositoryCount = 39;
            var output = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                output,
                new StringWriter(),
                new StringReader(""),
                inputRedirected: false,
                factory
            );

            var code = await dispatcher.RunAsync(["identity", "--verify", "-config", configPath]);
            Assert.Equal(0, code);
            Assert.Contains("\"repositories\":39", output.ToString());
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task GenerateWritesUtf8WithoutBom()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var outputPath = Path.Combine(workspace, "generated.gitconfig");
            var factory = new FakeGitHubAppClientFactory();
            factory.Client.Installations =
            [
                new GitHubInstallation(7, new GitHubAccount("acme", "https://github.com/acme")),
            ];
            var resolver = new FakeExecutableResolver { Prefix = ["gh-app"] };
            var error = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                new StringWriter(),
                error,
                new StringReader(""),
                inputRedirected: false,
                factory,
                resolver
            );

            var code = await dispatcher.RunAsync([
                "generate",
                "-config",
                configPath,
                "-output",
                outputPath,
            ]);
            Assert.Equal(0, code);
            var bytes = File.ReadAllBytes(outputPath);
            Assert.NotEqual(0xEF, bytes[0]);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.Contains("[credential \"https://github.com/acme\"]", text);
            Assert.Contains("!gh-app -appId 123", text);
            Assert.Contains("config written to", error.ToString());
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task SetupConfiguresRepositoryLocally()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var repository = Path.Combine(workspace, "repo");
            await TestFiles.CreateRepositoryAsync(repository, "https://github.com/acme/repo.git");
            var factory = new FakeGitHubAppClientFactory();
            var resolver = new FakeExecutableResolver { Prefix = ["gh-app"] };
            var output = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                output,
                new StringWriter(),
                new StringReader(""),
                inputRedirected: false,
                factory,
                resolver
            );

            var code = await dispatcher.RunAsync(["setup", "-config", configPath, repository]);
            Assert.Equal(0, code);
            Assert.Contains("Repository configured", output.ToString());

            var git = new GitRunner();
            var cancellationToken = TestContext.Current.CancellationToken;
            Assert.Equal(
                "orih-agent[bot]",
                (await git.GetAllConfigAsync(repository, "user.name", cancellationToken))[0]
            );
            Assert.Equal(
                "347995100+orih-agent[bot]@users.noreply.github.com",
                (await git.GetAllConfigAsync(repository, "user.email", cancellationToken))[0]
            );
            var general = await git.GetAllConfigAsync(
                repository,
                "credential.helper",
                cancellationToken
            );
            Assert.Equal("", general[0]);
            var github = await git.GetAllConfigAsync(
                repository,
                "credential.https://github.com.helper",
                cancellationToken
            );
            Assert.Equal("", github[0]);
            Assert.Contains(
                github,
                value => value.StartsWith("!gh-app -appId", StringComparison.Ordinal)
            );
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task SetupRejectsSshRemoteBeforeApi()
    {
        var workspace = NewWorkspace(out var configPath);
        try
        {
            var repository = Path.Combine(workspace, "repo");
            await TestFiles.CreateRepositoryAsync(repository, "git@github.com:acme/repo.git");
            var factory = new FakeGitHubAppClientFactory();
            var error = new StringWriter();
            var dispatcher = TestFiles.CreateDispatcher(
                new StringWriter(),
                error,
                new StringReader(""),
                inputRedirected: false,
                factory
            );

            var code = await dispatcher.RunAsync(["setup", "-config", configPath, repository]);
            Assert.Equal(1, code);
            Assert.Contains("HTTPS", error.ToString());
            Assert.Null(factory.LastSettings);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task VersionAndHelp()
    {
        var dispatcher = TestFiles.CreateDispatcher(
            new StringWriter(),
            new StringWriter(),
            new StringReader(""),
            inputRedirected: false,
            new FakeGitHubAppClientFactory()
        );
        Assert.Equal(0, await dispatcher.RunAsync(["version"]));
        Assert.Equal(1, await dispatcher.RunAsync([]));
        Assert.Equal(0, await dispatcher.RunAsync(["help"]));
    }

    private static string NewWorkspace(out string configPath)
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"gh-app-cs-dispatch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        var keyPath = TestKeys.WriteKeyFile(workspace);
        configPath = TestFiles.WriteConfig(workspace, keyPath);
        return workspace;
    }
}
