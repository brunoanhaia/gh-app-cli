using GhApp.Generate;
using GhApp.GitHub;

namespace GhApp.Tests;

public sealed class GenerateTests
{
    [Fact]
    public void GeneratesGithubSections()
    {
        var output = new StringWriter();
        var installations = new[]
        {
            new GitHubInstallation(7, new GitHubAccount("acme", "https://github.com/acme")),
        };
        GitConfigGenerator.Write(
            output,
            installations,
            new GenerateOptions("github.com", "x-access-token", 123, "C:/keys/pk.pem", ["gh-app"])
        );

        var text = output.ToString();
        Assert.Contains("[credential \"https://github.com/acme\"]", text);
        Assert.Contains("\tuseHttpPath = true", text);
        Assert.Contains(
            "\thelper = \"!gh-app -appId 123 -installationId 7 -privateKeyFile C:/keys/pk.pem -username x-access-token\"",
            text
        );
        Assert.Contains("[credential \"https://github.com\"]", text);
        Assert.Contains("\thelper = \"cache --timeout=43200\"", text);
        Assert.Contains("\tinsteadOf = git@github.com:", text);
        Assert.Contains("\tinsteadOf = ssh://git@github.com/", text);
        Assert.DoesNotContain("-domain", text);
    }

    [Fact]
    public void GeneratesGheWithPort()
    {
        var output = new StringWriter();
        var installations = new[]
        {
            new GitHubInstallation(
                8,
                new GitHubAccount("acme", "https://ghe.example.com:8443/acme")
            ),
        };
        GitConfigGenerator.Write(
            output,
            installations,
            new GenerateOptions(
                "ghe.example.com:8443",
                "x-access-token",
                123,
                "/home/user/pk.pem",
                ["gh-app"]
            )
        );

        var text = output.ToString();
        Assert.Contains("[credential \"https://ghe.example.com:8443/acme\"]", text);
        Assert.Contains("-domain ghe.example.com:8443", text);
        Assert.Contains("[credential \"https://ghe.example.com:8443\"]", text);
        Assert.Contains("\tinsteadOf = ssh://git@ghe.example.com:8443/", text);
        Assert.DoesNotContain("insteadOf = git@ghe.example.com:8443:", text);
    }

    [Fact]
    public void FallsBackToAccountLogin()
    {
        var output = new StringWriter();
        var installations = new[] { new GitHubInstallation(9, new GitHubAccount("acme", null)) };
        GitConfigGenerator.Write(
            output,
            installations,
            new GenerateOptions("github.com", "x-access-token", 1, "/pk.pem", ["gh-app"])
        );
        Assert.Contains("[credential \"https://github.com/acme\"]", output.ToString());
    }

    [Fact]
    public void EscapesGitValues()
    {
        Assert.Equal("a\\\"b\\\\c\\n\\t ", GitConfigGenerator.EscapeGit("a\"b\\c\n\t\r"));
    }
}
