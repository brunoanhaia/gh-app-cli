using GhApp.GitHub;

namespace GhApp.Credential;

internal sealed class CredentialRequest : Dictionary<string, string>
{
    public CredentialRequest()
        : base(StringComparer.Ordinal) { }
}

internal static class CredentialProtocol
{
    private static readonly char[] InvalidHostChars =
    [
        '/',
        '\\',
        '?',
        '#',
        '@',
        ' ',
        '\t',
        '\r',
        '\n',
    ];

    public static CredentialRequest Parse(TextReader reader)
    {
        var request = new CredentialRequest();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }
            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            request[key] = value;
        }
        return request;
    }

    // Prevent returning a GitHub token to another host.
    public static bool Allowed(CredentialRequest request, string domain)
    {
        domain = DomainInfo.Normalize(domain);
        if (domain.Length == 0)
        {
            domain = "github.com";
        }

        var protocol = Get(request, "protocol").ToLowerInvariant();
        if (protocol.Length > 0 && protocol != "https")
        {
            return false;
        }

        var host = Get(request, "host");
        if (host.Length == 0)
        {
            var rawUrl = Get(request, "url");
            if (rawUrl.Length > 0)
            {
                if (
                    !Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed)
                    || !string.Equals(parsed.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                )
                {
                    return false;
                }
                host = parsed.IsDefaultPort ? parsed.Host : $"{parsed.Host}:{parsed.Port}";
            }
        }
        if (host.Length == 0)
        {
            return true;
        }

        host = host.ToLowerInvariant();
        if (host == domain)
        {
            return true;
        }
        if (host.IndexOfAny(InvalidHostChars) >= 0)
        {
            return false;
        }

        if (
            !Uri.TryCreate("https://" + host, UriKind.Absolute, out var hostUri)
            || string.IsNullOrWhiteSpace(hostUri.Host)
        )
        {
            return false;
        }

        var hostWithPort = $"{hostUri.Host}:{hostUri.Port}";
        if (hostWithPort == domain)
        {
            return true;
        }

        // A domain without a port (github.com) may only receive a token on port 443.
        return string.Equals(hostUri.Host, domain, StringComparison.OrdinalIgnoreCase)
            && hostUri.Port == 443;
    }

    public static void WriteGet(
        TextWriter writer,
        string username,
        string token,
        DateTimeOffset expires
    )
    {
        username = Sanitize(username);
        token = Sanitize(token);
        if (expires == default)
        {
            expires = DateTimeOffset.UtcNow.AddHours(1);
        }
        writer.Write(
            $"username={username}\npassword={token}\npassword_expiry_utc={expires.ToUnixTimeSeconds()}\n"
        );
        writer.Flush();
    }

    public static void WriteQuit(TextWriter writer)
    {
        writer.Write("quit=1\n");
        writer.Flush();
    }

    private static string Get(CredentialRequest request, string key)
    {
        return request.TryGetValue(key, out var value) ? value.Trim() : "";
    }

    private static string Sanitize(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
