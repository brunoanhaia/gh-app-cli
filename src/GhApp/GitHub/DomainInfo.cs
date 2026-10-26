using GhApp.Support;

namespace GhApp.GitHub;

internal sealed record DomainInfo(
    string Domain,
    string Host,
    int? Port,
    string ApiBase,
    string WebBase,
    string EmailDomain
)
{
    private static readonly char[] InvalidDomainChars =
    [
        '/',
        ' ',
        '\t',
        '\r',
        '\n',
        '?',
        '#',
        '@',
        '\\',
        ',',
        ';',
    ];

    public static string Normalize(string domain)
    {
        domain = domain.Trim().ToLowerInvariant();
        domain = domain.TrimPrefix("https://").TrimPrefix("http://");
        domain = domain.TrimEnd('/');
        if (domain.EndsWith(":443", StringComparison.Ordinal))
        {
            domain = domain[..^4];
        }
        return domain;
    }

    public static DomainInfo Parse(string domain)
    {
        var normalized = Normalize(domain);
        Validate(normalized);

        if (!Uri.TryCreate("https://" + normalized, UriKind.Absolute, out var uri))
        {
            throw new GhAppException($"invalid domain: {domain}");
        }

        var host = uri.Host;
        var port = uri.IsDefaultPort ? (int?)null : uri.Port;
        var apiBase =
            normalized == "github.com" ? "https://api.github.com" : $"https://{normalized}/api/v3";
        var webBase = $"https://{normalized}";
        var emailDomain = host.Trim('[', ']');
        return new DomainInfo(normalized, host, port, apiBase, webBase, emailDomain);
    }

    public static void Validate(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new GhAppException("empty domain");
        }
        if (domain.IndexOfAny(InvalidDomainChars) >= 0)
        {
            throw new GhAppException($"invalid domain: {domain}");
        }
        if (
            !Uri.TryCreate("https://" + domain, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
        )
        {
            throw new GhAppException($"invalid domain: {domain}");
        }
    }
}

internal static class StringExtensions
{
    public static string TrimPrefix(this string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;
    }
}
