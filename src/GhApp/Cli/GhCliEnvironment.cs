using GhApp.GitHub;

namespace GhApp.Cli;

internal static class GhCliEnvironment
{
    public static IReadOnlyDictionary<string, string> Build(string domain, string token)
    {
        var normalized = DomainInfo.Normalize(domain);
        if (normalized == "github.com")
        {
            return new Dictionary<string, string>(StringComparer.Ordinal) { ["GH_TOKEN"] = token };
        }

        if (
            normalized == "ghe.com"
            || normalized.EndsWith(".ghe.com", StringComparison.OrdinalIgnoreCase)
        )
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GH_TOKEN"] = token,
                ["GH_HOST"] = normalized,
            };
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GH_ENTERPRISE_TOKEN"] = token,
            ["GH_HOST"] = normalized,
        };
    }
}
