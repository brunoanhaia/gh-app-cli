using System.Text.Json.Serialization;

namespace GhApp.GitHub;

internal sealed record GitHubAppInfo([property: JsonPropertyName("slug")] string? Slug);

internal sealed record GitHubUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("login")] string? Login
);

internal sealed record GitHubAccount(
    [property: JsonPropertyName("login")] string? Login,
    [property: JsonPropertyName("html_url")] string? HtmlUrl
);

internal sealed record GitHubInstallation(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("account")] GitHubAccount? Account
);

internal sealed record GitHubTokenResponse(
    [property: JsonPropertyName("token")] string? Token,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt
);

internal sealed record GitHubRepository([property: JsonPropertyName("full_name")] string? FullName);

internal sealed record GitHubRepositoriesResponse(
    [property: JsonPropertyName("total_count")] int TotalCount,
    [property: JsonPropertyName("repositories")] List<GitHubRepository>? Repositories
);

internal sealed record BotIdentity(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("login")] string Login,
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("repositories")] int? Repositories = null
);

internal sealed record InstallationToken(string Token, DateTimeOffset ExpiresAt);

internal sealed record RepositoryAccess(bool Found, int TotalCount);
