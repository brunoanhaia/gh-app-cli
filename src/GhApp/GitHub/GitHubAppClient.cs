using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GhApp.Settings;
using GhApp.Support;

namespace GhApp.GitHub;

internal sealed class GitHubAppClient : IGitHubAppClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppSettings _settings;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _apiBase;
    private readonly DomainInfo _domain;
    private long? _installationId;

    public GitHubAppClient(AppSettings settings, HttpClient? http = null, string? apiBase = null)
    {
        _settings = settings;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsHttp = http is null;
        _domain = DomainInfo.Parse(settings.Domain);
        _apiBase = (apiBase ?? _domain.ApiBase).TrimEnd('/');
    }

    public string ApiBase => _apiBase;

    public async Task<long> GetInstallationIdAsync(CancellationToken cancellationToken = default)
    {
        if (_installationId.HasValue)
        {
            return _installationId.Value;
        }
        if (_settings.InstallationId > 0)
        {
            _installationId = _settings.InstallationId;
            return _installationId.Value;
        }
        if (string.IsNullOrWhiteSpace(_settings.Organization))
        {
            throw new GhAppException("provide installationId or organization");
        }

        var jwt = CreateJwt();
        var path = "/orgs/" + Uri.EscapeDataString(_settings.Organization.Trim()) + "/installation";
        var installation = await SendAsync<GitHubInstallation>(
            HttpMethod.Get,
            path,
            jwt,
            cancellationToken
        );
        if (installation.Id <= 0)
        {
            throw new GhAppException("invalid installation returned by the API");
        }
        _installationId = installation.Id;
        return installation.Id;
    }

    public async Task<InstallationToken> CreateInstallationTokenAsync(
        long installationId,
        CancellationToken cancellationToken = default
    )
    {
        var jwt = CreateJwt();
        var path = $"/app/installations/{installationId}/access_tokens";
        var response = await SendAsync<GitHubTokenResponse>(
            HttpMethod.Post,
            path,
            jwt,
            cancellationToken
        );
        var token = response.Token?.Trim() ?? "";
        if (token.Length == 0)
        {
            throw new GhAppException("empty installation token returned by the API");
        }
        return new InstallationToken(
            token,
            response.ExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1)
        );
    }

    public async Task<BotIdentity> GetIdentityAsync(
        bool verify,
        CancellationToken cancellationToken = default
    )
    {
        var jwt = CreateJwt();
        var app = await SendAsync<GitHubAppInfo>(HttpMethod.Get, "/app", jwt, cancellationToken);
        var slug = app.Slug?.Trim() ?? "";
        if (slug.Length == 0)
        {
            throw new GhAppException("empty app slug returned by the API");
        }

        var login = slug + "[bot]";
        var userPath = "/users/" + Uri.EscapeDataString(login);
        var user = await SendAsync<GitHubUser>(HttpMethod.Get, userPath, null, cancellationToken);
        if (user.Id <= 0)
        {
            throw new GhAppException($"user {login} has no id returned by the API");
        }

        var email = $"{user.Id}+{login}@users.noreply.{_domain.EmailDomain}";

        int? repositories = null;
        if (verify)
        {
            var installationId = await GetInstallationIdAsync(cancellationToken);
            var token = await CreateInstallationTokenAsync(installationId, cancellationToken);
            repositories = await GetRepositoryCountAsync(token.Token, cancellationToken);
        }

        return new BotIdentity(slug, login, user.Id, email, repositories);
    }

    public async Task<IReadOnlyList<GitHubInstallation>> ListInstallationsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var jwt = CreateJwt();
        var result = new List<GitHubInstallation>();
        for (var page = 1; ; page++)
        {
            var path = $"/app/installations?per_page=100&page={page}";
            var pageItems = await SendAsync<List<GitHubInstallation>>(
                HttpMethod.Get,
                path,
                jwt,
                cancellationToken
            );
            result.AddRange(pageItems);
            if (pageItems.Count < 100)
            {
                break;
            }
        }
        return result;
    }

    public async Task<RepositoryAccess> RepoInInstallationAsync(
        string fullName,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var totalCount = 0;
        for (var page = 1; ; page++)
        {
            var path = $"/installation/repositories?per_page=100&page={page}";
            var response = await SendAsync<GitHubRepositoriesResponse>(
                HttpMethod.Get,
                path,
                token,
                cancellationToken
            );
            if (page == 1)
            {
                totalCount = response.TotalCount;
            }
            var repositories = response.Repositories ?? [];
            foreach (var repository in repositories)
            {
                if (
                    string.Equals(repository.FullName, fullName, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return new RepositoryAccess(true, totalCount);
                }
            }
            if (repositories.Count < 100)
            {
                break;
            }
        }
        return new RepositoryAccess(false, totalCount);
    }

    public async Task<int> GetRepositoryCountAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var response = await SendAsync<GitHubRepositoriesResponse>(
            HttpMethod.Get,
            "/installation/repositories?per_page=1",
            token,
            cancellationToken
        );
        return response.TotalCount;
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private string CreateJwt()
    {
        return JwtFactory.Create(_settings.AppId, _settings.PrivateKeyPem);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        string? bearer,
        CancellationToken cancellationToken
    )
    {
        var url = _apiBase + path;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("gh-app-dotnet/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(bearer))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubApiException(response.StatusCode, path, body);
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new GhAppException($"empty API response at {path}");
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
            if (result is null)
            {
                throw new GhAppException($"empty JSON response from the API at {path}");
            }
            return result;
        }
        catch (JsonException ex)
        {
            throw new GhAppException(
                $"invalid JSON response from the API at {path}: {ex.Message}",
                ex
            );
        }
    }
}
