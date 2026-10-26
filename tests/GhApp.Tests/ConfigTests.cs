using System.Text.Json;
using GhApp.Cli;
using GhApp.Config;
using GhApp.GitHub;
using GhApp.Settings;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class ConfigTests
{
    [Fact]
    public void FlexibleIdsAcceptNumberAndString()
    {
        var json = """
            {
              "appId": "123",
              "installationId": 456,
              "privateKeyPath": "pk.pem"
            }
            """;
        var config = JsonSerializer.Deserialize<AppConfig>(json);
        Assert.NotNull(config);
        Assert.Equal(123, config.AppId);
        Assert.Equal(456, config.InstallationId);
        Assert.Equal("pk.pem", config.PrivateKeyPath);
    }

    [Fact]
    public void PrivateKeyFileAliasWorks()
    {
        var json = """{"appId":1,"installationId":2,"privateKeyFile":"alias.pem"}""";
        var config = JsonSerializer.Deserialize<AppConfig>(json);
        Assert.NotNull(config);
        Assert.Equal("alias.pem", config.EffectivePrivateKeyPath);
    }

    [Fact]
    public void SettingsResolveRelativeKeyAgainstConfigDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "gh-app-cs-config-" + Guid.NewGuid().ToString("N")
        );
        var keyPath = TestKeys.WriteKeyFile(directory);
        var configPath = TestFiles.WriteConfig(directory, "pk.pem");
        var parsed = new ParsedArgs(
            new Dictionary<string, string?> { ["config"] = configPath },
            [],
            false
        );

        var settings = SettingsResolver.Resolve(parsed);
        Assert.Equal(123, settings.AppId);
        Assert.Equal(456, settings.InstallationId);
        Assert.Equal(Path.GetFullPath(keyPath), settings.PrivateKeyFile);
        Assert.Equal("github.com", settings.Domain);
        settings.ValidateAuth();
        Assert.NotEmpty(settings.PrivateKeyPem);
    }

    [Fact]
    public void FlagsOverrideConfig()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "gh-app-cs-config-" + Guid.NewGuid().ToString("N")
        );
        var keyPath = TestKeys.WriteKeyFile(directory);
        var configPath = TestFiles.WriteConfig(directory, keyPath, appId: 1, installationId: 2);
        var parsed = new ParsedArgs(
            new Dictionary<string, string?>
            {
                ["config"] = configPath,
                ["appId"] = "999",
                ["organization"] = "acme",
                ["username"] = "custom",
                ["domain"] = "https://GHE.Example.com/",
            },
            [],
            false
        );

        var settings = SettingsResolver.Resolve(parsed);
        Assert.Equal(999, settings.AppId);
        Assert.Equal(2, settings.InstallationId);
        Assert.Equal("acme", settings.Organization);
        Assert.Equal("custom", settings.Username);
        Assert.Equal("ghe.example.com", settings.Domain);
        Assert.Equal(Path.GetFullPath(keyPath), settings.PrivateKeyFile);
    }

    [Fact]
    public void InvalidConfigJsonIsReported()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "gh-app-cs-config-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        File.WriteAllText(path, "{ not-json }");
        var parsed = new ParsedArgs(
            new Dictionary<string, string?> { ["config"] = path },
            [],
            false
        );

        var error = Assert.Throws<GhAppException>(() => SettingsResolver.Resolve(parsed));
        Assert.Contains("invalid config.json", error.Message);
    }

    [Fact]
    public void DomainNormalizationIsApplied()
    {
        Assert.Equal("github.com", DomainInfo.Normalize(" HTTPS://GitHub.com:443/ "));
        Assert.Equal("ghe.example.com:8443", DomainInfo.Normalize("https://GHE.Example.com:8443/"));
    }
}
