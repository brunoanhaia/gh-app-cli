using GhApp.Git;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class RemoteUrlTests
{
    [Fact]
    public void ParsesGithubHttps()
    {
        var remote = RemoteUrlParser.Parse("https://github.com/Owner/Repo.git", "github.com");
        Assert.Equal("Owner", remote.Owner);
        Assert.Equal("Repo", remote.Repository);
        Assert.Equal("Owner/Repo", remote.FullName);
        Assert.Equal("github.com", remote.Host);
    }

    [Fact]
    public void ParsesGheWithPort()
    {
        var remote = RemoteUrlParser.Parse(
            "https://ghe.example.com:8443/owner/repo/",
            "ghe.example.com:8443"
        );
        Assert.Equal("owner/repo", remote.FullName);
        Assert.Equal("ghe.example.com", remote.Host);
    }

    [Theory]
    [InlineData("git@github.com:owner/repo.git", "github.com")]
    [InlineData("ssh://git@github.com/owner/repo.git", "github.com")]
    [InlineData("http://github.com/owner/repo.git", "github.com")]
    [InlineData("https://user@github.com/owner/repo.git", "github.com")]
    [InlineData("https://github.com/owner/repo?x=1", "github.com")]
    [InlineData("https://github.com/owner/repo#frag", "github.com")]
    [InlineData("https://github.com/owner/repo/extra", "github.com")]
    [InlineData("https://gitlab.com/owner/repo", "github.com")]
    [InlineData("https://github.com/owner//repo", "github.com")]
    [InlineData("https://github.com/owner/%2Frepo", "github.com")]
    [InlineData("https://github.com/owner/..", "github.com")]
    [InlineData("https://github.com:8443/owner/repo", "github.com")]
    [InlineData("https://ghe.example.com/owner/repo", "ghe.example.com:8443")]
    public void RejectsInvalidRemotes(string url, string domain)
    {
        Assert.Throws<GhAppException>(() => RemoteUrlParser.Parse(url, domain));
    }
}
