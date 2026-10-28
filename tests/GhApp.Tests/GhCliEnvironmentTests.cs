using GhApp.Cli;

namespace GhApp.Tests;

public sealed class GhCliEnvironmentTests
{
    [Theory]
    [InlineData("github.com", "GH_TOKEN", null, "GH_ENTERPRISE_TOKEN")]
    [InlineData("https://github.com:443/", "GH_TOKEN", null, "GH_ENTERPRISE_TOKEN")]
    [InlineData("acme.ghe.com", "GH_TOKEN", "acme.ghe.com", "GH_ENTERPRISE_TOKEN")]
    [InlineData("ghe.example.com", "GH_ENTERPRISE_TOKEN", "ghe.example.com", "GH_TOKEN")]
    public void MapsTokenAndHostForEachDomain(
        string domain,
        string tokenVariable,
        string? expectedHost,
        string otherTokenVariable
    )
    {
        var environment = GhCliEnvironment.Build(domain, "ghs_test");

        Assert.Equal("ghs_test", environment[tokenVariable]);
        Assert.False(environment.ContainsKey(otherTokenVariable));
        if (expectedHost is null)
        {
            Assert.False(environment.ContainsKey("GH_HOST"));
        }
        else
        {
            Assert.Equal(expectedHost, environment["GH_HOST"]);
        }
    }
}
