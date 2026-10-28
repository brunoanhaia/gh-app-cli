using GhApp.Credential;
using GhApp.Git;

namespace GhApp.Tests;

public sealed class GitCredentialCacheTests
{
    [Fact]
    public async Task StoresAndRetrievesTokenByKey()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"gh-app-cache-{Guid.NewGuid():N}.sock");
        var cache = new GitCredentialCache(socketPath);
        var cancellationToken = TestContext.Current.CancellationToken;
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        try
        {
            await cache.StoreAsync(
                "github.com",
                "gh-app/123/456",
                "x-access-token",
                "ghs_test",
                expires,
                cancellationToken
            );

            var value = await cache.GetAsync("github.com", "gh-app/123/456", cancellationToken);

            Assert.NotNull(value);
            Assert.Equal("ghs_test", value.Token);
            Assert.NotNull(value.ExpiresAt);
            Assert.Equal(expires.ToUnixTimeSeconds(), value.ExpiresAt.Value.ToUnixTimeSeconds());
            Assert.Null(await cache.GetAsync("github.com", "gh-app/123/999", cancellationToken));
        }
        finally
        {
            await new GitRunner().RunAsync(
                null,
                ["credential-cache", $"--socket={socketPath}", "exit"],
                cancellationToken
            );
        }
    }

    [Fact]
    public void ParsesCacheOutput()
    {
        var value = GitCredentialCache.ParseGet(
            "capability[]=authtype\nusername=x-access-token\npassword=ghs_test\npassword_expiry_utc=1900000000\n"
        );

        Assert.NotNull(value);
        Assert.Equal("ghs_test", value.Token);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_900_000_000), value.ExpiresAt);
        Assert.Null(GitCredentialCache.ParseGet("username=x-access-token\n"));
    }
}
