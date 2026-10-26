using GhApp.GitHub;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class DomainTests
{
    [Fact]
    public void GithubDomainProducesGithubApiAndEmail()
    {
        var info = DomainInfo.Parse("github.com");
        Assert.Equal("github.com", info.Domain);
        Assert.Equal("github.com", info.Host);
        Assert.Null(info.Port);
        Assert.Equal("https://api.github.com", info.ApiBase);
        Assert.Equal("https://github.com", info.WebBase);
        Assert.Equal("github.com", info.EmailDomain);
    }

    [Fact]
    public void GheDomainWithPort()
    {
        var info = DomainInfo.Parse("https://GHE.Example.com:8443/");
        Assert.Equal("ghe.example.com:8443", info.Domain);
        Assert.Equal("ghe.example.com", info.Host);
        Assert.Equal(8443, info.Port);
        Assert.Equal("https://ghe.example.com:8443/api/v3", info.ApiBase);
        Assert.Equal("ghe.example.com", info.EmailDomain);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://github.com")]
    [InlineData("github.com?x")]
    [InlineData("github.com#x")]
    [InlineData("github.com@evil")]
    [InlineData("github.com\\evil")]
    [InlineData("github.com,evil")]
    [InlineData("github.com;evil")]
    public void InvalidDomainsAreRejected(string domain)
    {
        Assert.Throws<GhAppException>(() => DomainInfo.Validate(domain));
    }
}
