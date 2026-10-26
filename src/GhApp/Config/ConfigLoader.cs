using System.Text.Json;
using GhApp.Support;

namespace GhApp.Config;

internal sealed record LoadedConfig(string? Path, AppConfig Config);

internal static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static LoadedConfig Load(string? explicitPath)
    {
        var path = ResolvePath(explicitPath);
        if (path is null)
        {
            return new LoadedConfig(null, new AppConfig());
        }

        try
        {
            var text = File.ReadAllText(path);
            var config =
                JsonSerializer.Deserialize<AppConfig>(text, JsonOptions) ?? new AppConfig();
            return new LoadedConfig(path, config);
        }
        catch (JsonException ex)
        {
            throw new GhAppException($"invalid config.json at {path}: {ex.Message}", ex);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            throw new GhAppException($"could not read config.json at {path}: {ex.Message}", ex);
        }
    }

    public static string? ResolvePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return ToFullPath(explicitPath);
        }

        var environmentPath = Environment.GetEnvironmentVariable("GH_APP_CONFIG");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            return ToFullPath(environmentPath);
        }

        foreach (var candidate in DefaultCandidates())
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    public static string UserConfigPath()
    {
        string baseDirectory;
        if (OperatingSystem.IsWindows())
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            baseDirectory = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config"
                );
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return "";
        }
        return Path.Combine(baseDirectory, "gh-app", "config.json");
    }

    private static IEnumerable<string> DefaultCandidates()
    {
        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(executable));
            if (!string.IsNullOrEmpty(directory))
            {
                yield return Path.Combine(directory, "config.json");
                var parent = Directory.GetParent(directory)?.FullName;
                if (!string.IsNullOrEmpty(parent))
                {
                    yield return Path.Combine(parent, "config.json");
                }
            }
        }

        var userConfig = UserConfigPath();
        if (!string.IsNullOrEmpty(userConfig))
        {
            yield return userConfig;
        }
    }

    private static string ToFullPath(string path)
    {
        return Path.GetFullPath(PathExpander.ExpandHome(path));
    }
}
