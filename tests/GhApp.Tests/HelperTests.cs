using GhApp.Helper;

namespace GhApp.Tests;

public sealed class HelperTests
{
    [Fact]
    public void BuildsCommandWithQuotingAndGheDomain()
    {
        var command = HelperCommand.Build(
            [@"C:\Program Files\gh-app\gh-app.exe"],
            new HelperSpec(
                123,
                456,
                @"C:\Users\Bruno\gh app\pk.pem",
                "orih-agent[bot]",
                "ghe.example.com:8443"
            )
        );

        Assert.Equal(
            "!'C:/Program Files/gh-app/gh-app.exe' -appId 123 -installationId 456 -privateKeyFile 'C:/Users/Bruno/gh app/pk.pem' -username 'orih-agent[bot]' -domain ghe.example.com:8443",
            command
        );
    }

    [Fact]
    public void OmitsGithubDomain()
    {
        var command = HelperCommand.Build(
            ["gh-app"],
            new HelperSpec(1, 2, "/home/user/pk.pem", "x-access-token", "github.com")
        );
        Assert.DoesNotContain("-domain", command);
        Assert.Contains("-username x-access-token", command);
    }

    [Fact]
    public void SupportsDotnetAssemblyPrefix()
    {
        var command = HelperCommand.Build(
            ["dotnet", @"C:\app\gh-app.dll"],
            new HelperSpec(1, 2, "/home/user/pk.pem", "x-access-token", "github.com")
        );
        Assert.StartsWith("!dotnet C:/app/gh-app.dll -appId", command);
    }

    [Fact]
    public void EscapesSingleQuotes()
    {
        Assert.Equal("'a'\\''b'", HelperCommand.ShellQuote("a'b"));
    }

    [Fact]
    public void HelperNameOverrideIsUsed()
    {
        var resolver = new ExecutableResolver();
        Assert.Equal(["gh-app"], resolver.FromHelperName(" gh-app "));
    }
}
