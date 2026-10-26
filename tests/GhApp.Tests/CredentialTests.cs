using GhApp.Credential;

namespace GhApp.Tests;

public sealed class CredentialTests
{
    [Fact]
    public void ParsesCrlfRequest()
    {
        var request = CredentialProtocol.Parse(
            new StringReader("protocol=https\r\nhost=github.com\r\nusername=foo\r\n\r\n")
        );
        Assert.Equal("https", request["protocol"]);
        Assert.Equal("github.com", request["host"]);
        Assert.Equal("foo", request["username"]);
    }

    [Theory]
    [InlineData("https", "github.com", "github.com", true)]
    [InlineData("https", "github.com:443", "github.com", true)]
    [InlineData("https", "GitHub.com", "github.com", true)]
    [InlineData("https", "gitlab.com", "github.com", false)]
    [InlineData("http", "github.com", "github.com", false)]
    [InlineData("https", "github.com:8443", "github.com", false)]
    [InlineData("https", "github.com/evil", "github.com", false)]
    [InlineData("https", "github.com:443@evil.com", "github.com", false)]
    [InlineData("https", "ghe.example.com", "ghe.example.com", true)]
    [InlineData("https", "ghe.example.com:8443", "ghe.example.com:8443", true)]
    public void HostCheck(string protocol, string host, string domain, bool expected)
    {
        var request = new CredentialRequest { ["protocol"] = protocol, ["host"] = host };
        Assert.Equal(expected, CredentialProtocol.Allowed(request, domain));
    }

    [Fact]
    public void UrlCheck()
    {
        Assert.True(
            CredentialProtocol.Allowed(
                new CredentialRequest
                {
                    ["protocol"] = "https",
                    ["url"] = "https://github.com/acme/repo",
                },
                "github.com"
            )
        );
        Assert.False(
            CredentialProtocol.Allowed(
                new CredentialRequest { ["url"] = "http://github.com/acme/repo" },
                "github.com"
            )
        );
        Assert.False(
            CredentialProtocol.Allowed(
                new CredentialRequest { ["url"] = "ssh://git@github.com/acme/repo" },
                "github.com"
            )
        );
    }

    [Fact]
    public void MissingHostIsAllowed()
    {
        Assert.True(
            CredentialProtocol.Allowed(
                new CredentialRequest { ["protocol"] = "https" },
                "github.com"
            )
        );
    }

    [Fact]
    public void WriteGetAndQuit()
    {
        var writer = new StringWriter();
        CredentialProtocol.WriteGet(
            writer,
            "x-access-token",
            "ghs_test",
            DateTimeOffset.FromUnixTimeSeconds(1_900_000_000)
        );
        Assert.Equal(
            "username=x-access-token\npassword=ghs_test\npassword_expiry_utc=1900000000\n",
            writer.ToString()
        );

        var quit = new StringWriter();
        CredentialProtocol.WriteQuit(quit);
        Assert.Equal("quit=1\n", quit.ToString());
    }

    [Fact]
    public void WriteGetSanitizesNewlines()
    {
        var writer = new StringWriter();
        CredentialProtocol.WriteGet(
            writer,
            "user\nname",
            "token\r\n",
            DateTimeOffset.FromUnixTimeSeconds(1)
        );
        Assert.DoesNotContain("user\nname", writer.ToString());
        Assert.Equal(3, writer.ToString().Count(c => c == '\n'));
    }
}
