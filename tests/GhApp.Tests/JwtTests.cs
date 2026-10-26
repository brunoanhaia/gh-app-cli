using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GhApp.GitHub;
using GhApp.Support;

namespace GhApp.Tests;

public sealed class JwtTests
{
    [Fact]
    public void CreatesValidRs256Jwt()
    {
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var jwt = JwtFactory.Create(5471294, TestKeys.PrivateKeyBytes, now);
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);

        var header = JsonDocument.Parse(Base64UrlDecode(parts[0])).RootElement;
        var payload = JsonDocument.Parse(Base64UrlDecode(parts[1])).RootElement;
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());
        Assert.Equal(5471294, payload.GetProperty("iss").GetInt64());
        Assert.Equal(
            now.AddSeconds(-60).ToUnixTimeSeconds(),
            payload.GetProperty("iat").GetInt64()
        );
        Assert.Equal(now.AddMinutes(9).ToUnixTimeSeconds(), payload.GetProperty("exp").GetInt64());

        using var rsa = RSA.Create();
        rsa.ImportFromPem(TestKeys.PrivateKeyPem);
        var signingInput = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        var signature = Base64UrlDecode(parts[2]);
        Assert.True(
            rsa.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
        );
    }

    [Fact]
    public void InvalidKeyIsReported()
    {
        Assert.Throws<GhAppException>(() =>
            JwtFactory.Create(1, Encoding.UTF8.GetBytes("not-a-key"))
        );
    }

    private static byte[] Base64UrlDecode(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        value += new string('=', (4 - value.Length % 4) % 4);
        return Convert.FromBase64String(value);
    }
}
