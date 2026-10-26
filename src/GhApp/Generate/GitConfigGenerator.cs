using GhApp.GitHub;
using GhApp.Helper;

namespace GhApp.Generate;

internal sealed record GenerateOptions(
    string Domain,
    string Username,
    long AppId,
    string PrivateKeyFile,
    IReadOnlyList<string> HelperPrefix
);

internal static class GitConfigGenerator
{
    public static void Write(
        TextWriter writer,
        IReadOnlyList<GitHubInstallation> installations,
        GenerateOptions options
    )
    {
        var domain = options.Domain.Trim().ToLowerInvariant();
        if (domain.Length == 0)
        {
            domain = "github.com";
        }
        var username = string.IsNullOrWhiteSpace(options.Username)
            ? "x-access-token"
            : options.Username;
        var prefix = options.HelperPrefix.Count == 0 ? new[] { "gh-app" } : options.HelperPrefix;

        foreach (var installation in installations)
        {
            var account = installation.Account;
            var context = account?.HtmlUrl?.Trim().TrimEnd('/') ?? "";
            if (context.Length == 0)
            {
                var login = account?.Login?.Trim() ?? "";
                context = login.Length == 0 ? $"https://{domain}" : $"https://{domain}/{login}";
            }

            var command = HelperCommand.Build(
                prefix,
                new HelperSpec(
                    options.AppId,
                    installation.Id,
                    options.PrivateKeyFile,
                    username,
                    domain
                )
            );

            writer.Write($"[credential \"{EscapeGit(context)}\"]\n");
            writer.Write("\tuseHttpPath = true\n");
            writer.Write($"\thelper = \"{EscapeGit(command)}\"\n");
        }

        writer.Write($"[credential \"https://{EscapeGit(domain)}\"]\n");
        writer.Write("\thelper = \"cache --timeout=43200\"\n");
        writer.Write($"[url \"https://{EscapeGit(domain)}\"]\n");
        if (!domain.Contains(':'))
        {
            writer.Write($"\tinsteadOf = git@{EscapeGit(domain)}:\n");
        }
        writer.Write($"\tinsteadOf = ssh://git@{EscapeGit(domain)}/\n");
    }

    internal static string EscapeGit(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\r", " ");
    }
}
