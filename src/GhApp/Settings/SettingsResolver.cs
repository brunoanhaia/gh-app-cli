using System.Globalization;
using GhApp.Cli;
using GhApp.Config;
using GhApp.GitHub;
using GhApp.Support;

namespace GhApp.Settings;

internal static class SettingsResolver
{
    public static AppSettings Resolve(ParsedArgs parsed)
    {
        var loaded = ConfigLoader.Load(parsed.Value("config"));
        var config = loaded.Config;

        var settings = new AppSettings
        {
            Organization = (config.Organization ?? "").Trim(),
            Username = (config.Username ?? "").Trim(),
            Domain = (config.Domain ?? "").Trim(),
            ConfigPath = loaded.Path,
        };

        var appIdRaw = config.AppId.ToString(CultureInfo.InvariantCulture);
        if (parsed.IsSet("appId"))
        {
            appIdRaw = parsed.Value("appId") ?? "";
        }
        settings.AppId = ParseId(appIdRaw, "appId");

        var installationRaw = config.InstallationId.ToString(CultureInfo.InvariantCulture);
        if (parsed.IsSet("installationId"))
        {
            installationRaw = parsed.Value("installationId") ?? "";
        }
        settings.InstallationId = ParseId(installationRaw, "installationId");

        if (parsed.IsSet("organization"))
        {
            settings.Organization = (parsed.Value("organization") ?? "").Trim();
        }
        if (parsed.IsSet("username"))
        {
            settings.Username = (parsed.Value("username") ?? "").Trim();
        }
        if (parsed.IsSet("domain"))
        {
            settings.Domain = (parsed.Value("domain") ?? "").Trim();
        }

        if (parsed.IsSet("privateKeyFile"))
        {
            var raw = (parsed.Value("privateKeyFile") ?? "").Trim();
            if (raw.Length > 0)
            {
                settings.PrivateKeyFile = Path.GetFullPath(PathExpander.ExpandHome(raw));
            }
        }
        else if (!string.IsNullOrWhiteSpace(config.EffectivePrivateKeyPath))
        {
            var raw = PathExpander.ExpandHome(config.EffectivePrivateKeyPath.Trim());
            if (Path.IsPathRooted(raw))
            {
                settings.PrivateKeyFile = Path.GetFullPath(raw);
            }
            else
            {
                var directory = loaded.Path is null ? "" : Path.GetDirectoryName(loaded.Path) ?? "";
                if (directory.Length == 0)
                {
                    throw new GhAppException(
                        "relative private key path without a reference config.json"
                    );
                }
                settings.PrivateKeyFile = Path.GetFullPath(Path.Combine(directory, raw));
            }
        }

        if (string.IsNullOrWhiteSpace(settings.Username))
        {
            settings.Username = "x-access-token";
        }
        if (string.IsNullOrWhiteSpace(settings.Domain))
        {
            settings.Domain = "github.com";
        }
        settings.Domain = DomainInfo.Normalize(settings.Domain);
        DomainInfo.Validate(settings.Domain);
        return settings;
    }

    private static long ParseId(string raw, string field)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            return 0;
        }
        if (
            !long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < 0
        )
        {
            throw new GhAppException($"invalid {field}: {raw}");
        }
        return value;
    }
}
