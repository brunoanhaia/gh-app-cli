using System.Net;
using System.Text.Json;
using GhApp.GitHub;
using GhApp.Settings;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task IdentityWithoutVerifyUsesJwtAndPublicUser()
    {
        var settings = Settings();
        using var http = new HttpClient(
            new StubHttpMessageHandler(request =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path == "/app")
                {
                    Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                    Assert.StartsWith("eyJ", request.Headers.Authorization?.Parameter);
                    return HttpResponses.Json("""{"slug":"orih-agent"}""");
                }
                if (path.StartsWith("/users/", StringComparison.Ordinal))
                {
                    Assert.Null(request.Headers.Authorization);
                    Assert.Contains(
                        "orih-agent[bot]",
                        Uri.UnescapeDataString(path),
                        StringComparison.Ordinal
                    );
                    return HttpResponses.Json("""{"id":347995100,"login":"orih-agent[bot]"}""");
                }
                return HttpResponses.Status(HttpStatusCode.NotFound);
            })
        );
        using var client = new GitHubAppClient(settings, http, "https://api.test");

        var identity = await client.GetIdentityAsync(false, TestContext.Current.CancellationToken);
        Assert.Equal("orih-agent", identity.Slug);
        Assert.Equal("orih-agent[bot]", identity.Login);
        Assert.Equal(347995100, identity.Id);
        Assert.Equal("347995100+orih-agent[bot]@users.noreply.github.com", identity.Email);
        Assert.Null(identity.Repositories);
    }

    [Fact]
    public async Task IdentityWithVerifyCountsRepositories()
    {
        var settings = Settings();
        using var http = new HttpClient(
            new StubHttpMessageHandler(request =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path == "/app")
                {
                    return HttpResponses.Json("""{"slug":"orih-agent"}""");
                }
                if (path.StartsWith("/users/", StringComparison.Ordinal))
                {
                    return HttpResponses.Json("""{"id":347995100,"login":"orih-agent[bot]"}""");
                }
                if (path == "/app/installations/123/access_tokens")
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.StartsWith("eyJ", request.Headers.Authorization?.Parameter);
                    return HttpResponses.Json(
                        """{"token":"ghs_test","expires_at":"2026-10-25T13:00:00Z"}"""
                    );
                }
                if (path == "/installation/repositories")
                {
                    Assert.Contains("per_page=1", request.RequestUri!.Query);
                    Assert.Equal("ghs_test", request.Headers.Authorization?.Parameter);
                    return HttpResponses.Json("""{"total_count":39,"repositories":[]}""");
                }
                return HttpResponses.Status(HttpStatusCode.NotFound);
            })
        );
        using var client = new GitHubAppClient(settings, http, "https://api.test");

        var identity = await client.GetIdentityAsync(true, TestContext.Current.CancellationToken);
        Assert.Equal(39, identity.Repositories);
    }

    [Fact]
    public async Task ResolvesInstallationByOrganization()
    {
        var settings = Settings(installationId: 0, organization: "acme");
        using var http = new HttpClient(
            new StubHttpMessageHandler(request =>
            {
                Assert.Equal("/orgs/acme/installation", request.RequestUri!.AbsolutePath);
                return HttpResponses.Json(
                    """{"id":456,"account":{"login":"acme","html_url":"https://github.com/acme"}}"""
                );
            })
        );
        using var client = new GitHubAppClient(settings, http, "https://api.test");
        Assert.Equal(
            456,
            await client.GetInstallationIdAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ListsInstallationsWithPagination()
    {
        using var http = new HttpClient(
            new StubHttpMessageHandler(request =>
            {
                var page = request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal)
                    ? 2
                    : 1;
                var items =
                    page == 1
                        ? Enumerable
                            .Range(1, 100)
                            .Select(i => new
                            {
                                id = i,
                                account = new
                                {
                                    login = $"u{i}",
                                    html_url = $"https://github.com/u{i}",
                                },
                            })
                        : new[]
                        {
                            new
                            {
                                id = 101,
                                account = new
                                {
                                    login = "u101",
                                    html_url = "https://github.com/u101",
                                },
                            },
                        };
                return HttpResponses.Json(JsonSerializer.Serialize(items));
            })
        );
        using var client = new GitHubAppClient(Settings(), http, "https://api.test");
        var installations = await client.ListInstallationsAsync(
            TestContext.Current.CancellationToken
        );
        Assert.Equal(101, installations.Count);
    }

    [Fact]
    public async Task FindsRepositoryOnLaterPage()
    {
        using var http = new HttpClient(
            new StubHttpMessageHandler(request =>
            {
                var page = request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal)
                    ? 2
                    : 1;
                if (page == 1)
                {
                    var repositories = Enumerable
                        .Range(1, 100)
                        .Select(i => new { full_name = $"acme/repo{i}" })
                        .ToArray();
                    return HttpResponses.Json(
                        JsonSerializer.Serialize(new { total_count = 101, repositories })
                    );
                }
                return HttpResponses.Json(
                    """{"total_count":101,"repositories":[{"full_name":"acme/target"}]}"""
                );
            })
        );
        using var client = new GitHubAppClient(Settings(), http, "https://api.test");
        var access = await client.RepoInInstallationAsync(
            "acme/target",
            "ghs_test",
            TestContext.Current.CancellationToken
        );
        Assert.True(access.Found);
        Assert.Equal(101, access.TotalCount);
    }

    [Fact]
    public async Task ApiErrorIncludesStatusAndBody()
    {
        using var http = new HttpClient(
            new StubHttpMessageHandler(_ =>
                HttpResponses.Json(
                    """{"message":"Resource not accessible by integration"}""",
                    HttpStatusCode.Forbidden
                )
            )
        );
        using var client = new GitHubAppClient(Settings(), http, "https://api.test");
        var error = await Assert.ThrowsAsync<GitHubApiException>(() =>
            client.CreateInstallationTokenAsync(123, TestContext.Current.CancellationToken)
        );
        Assert.Equal(403, error.StatusCode);
        Assert.Contains("Resource not accessible", error.Message);
    }

    [Fact]
    public void GheApiBaseUsesApiV3()
    {
        var settings = Settings(domain: "ghe.example.com:8443");
        using var client = new GitHubAppClient(settings);
        Assert.Equal("https://ghe.example.com:8443/api/v3", client.ApiBase);
    }

    private static AppSettings Settings(
        long installationId = 123,
        string domain = "github.com",
        string? organization = null
    )
    {
        return new AppSettings
        {
            AppId = 1,
            InstallationId = installationId,
            Organization = organization ?? "",
            PrivateKeyPem = TestKeys.PrivateKeyBytes,
            Domain = domain,
            Username = "x-access-token",
        };
    }
}
