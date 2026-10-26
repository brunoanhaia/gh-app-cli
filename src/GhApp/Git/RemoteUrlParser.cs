using System.Text.RegularExpressions;
using GhApp.GitHub;
using GhApp.Support;

namespace GhApp.Git;

internal sealed record RemoteInfo(string Host, string Owner, string Repository, string FullName);

internal static partial class RemoteUrlParser
{
    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    public static RemoteInfo Parse(string url, string domain)
    {
        var domainInfo = DomainInfo.Parse(domain);
        var trimmed = url.Trim();
        if (
            trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("git+ssh://", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new GhAppException(
                "the origin remote must use HTTPS (installation tokens do not work over SSH)"
            );
        }
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new GhAppException($"invalid remote: {url}");
        }
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new GhAppException(
                "the origin remote must use HTTPS (installation tokens do not work over SSH)"
            );
        }
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new GhAppException(
                "the origin remote cannot contain a username/password in the URL"
            );
        }
        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new GhAppException("the origin remote cannot contain a query or fragment");
        }
        if (!string.Equals(uri.Host, domainInfo.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new GhAppException(
                $"the origin remote must point to {domainInfo.Host}, not {uri.Host}"
            );
        }

        if (domainInfo.Port is int expectedPort)
        {
            if (uri.Port != expectedPort)
            {
                throw new GhAppException($"the origin remote must use port {expectedPort}");
            }
        }
        else if (!uri.IsDefaultPort && uri.Port != 443)
        {
            throw new GhAppException("the origin remote must use HTTPS on the default port");
        }

        var rawPath = uri.AbsolutePath;
        if (rawPath.Contains("%2f", StringComparison.OrdinalIgnoreCase))
        {
            throw new GhAppException("the origin remote has an invalid path");
        }
        var path = rawPath.Trim('/');
        var parts = path.Split('/', StringSplitOptions.None);
        if (parts.Length != 2)
        {
            throw new GhAppException(
                "the origin remote must use the format https://host/owner/repo.git"
            );
        }

        var owner = Uri.UnescapeDataString(parts[0]);
        var repository = Uri.UnescapeDataString(parts[1]);
        if (
            repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            && repository.Length > 4
        )
        {
            repository = repository[..^4];
        }

        if (
            owner is "." or ".."
            || repository is "." or ".."
            || !NamePattern().IsMatch(owner)
            || !NamePattern().IsMatch(repository)
        )
        {
            throw new GhAppException("invalid owner/repository in the origin remote");
        }

        return new RemoteInfo(uri.Host, owner, repository, $"{owner}/{repository}");
    }
}
