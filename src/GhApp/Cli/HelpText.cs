namespace GhApp.Cli;

internal static class HelpText
{
    public const string General = """
gh-app - local GitHub App for commits and push

Usage:
  gh-app identity [--verify] [flags]
  gh-app token [flags]
  gh-app credential [flags] get|store|erase|generate
  gh-app get|store|erase
  gh-app generate [flags]
  gh-app setup [flags] <repo>
  gh-app version [--verbose]
  gh-app help

Common flags:
  -config PATH          JSON configuration file
  -appId ID             GitHub App ID
  -installationId ID    installation ID
  -organization ORG     organization used to discover the installation
  -privateKeyFile PATH  private key path
  -username NAME        credential username (default x-access-token)
  -domain DOMAIN        GitHub Enterprise domain (default github.com)

generate flags:
  -output PATH          write the gitconfig as UTF-8 (avoids the PowerShell BOM)
  -helper-name NAME     gh-app binary name/path used in the generated helper

Examples:
  gh-app identity --verify
  gh-app generate -output gh-app.gitconfig
  gh-app setup --config config.json C:\workspace\repos\my-repo
""";

    public const string Identity = """
Usage: gh-app identity [--verify] [flags]

Prints the bot identity as JSON. With --verify, it also creates an installation
token and counts the repositories visible to the installation. The token is not
printed.
""";

    public const string Token = """
Usage: gh-app token [flags]

Prints only the installation token, without a newline. Use with care: any
redirection may write the token to disk.
""";

    public const string Credential = """
Usage: gh-app credential [flags] get|store|erase|generate

  get       reads the request from stdin and returns username/password
  store     no-op
  erase     no-op
  generate  alias for gh-app generate

Direct custom-helper form: gh-app get|store|erase.
Unknown helper operations are ignored.

Flags: common flags, plus -output and -helper-name.
""";

    public const string Generate = """
Usage: gh-app generate [flags]

Generates a gitconfig with one helper per installation, useHttpPath, a 43200 s
cache, and an SSH -> HTTPS redirect. Use -output PATH to write UTF-8 without a
BOM.
""";

    public const string Setup = """
Usage: gh-app setup [flags] <repo>

Configures user.name/user.email and the credential helper only in the specified
repository. Requires an HTTPS origin remote and validates the exact repository
in the installation. It does not commit or push.
""";

    public const string Version = """
Usage: gh-app version [--verbose]
""";
}
