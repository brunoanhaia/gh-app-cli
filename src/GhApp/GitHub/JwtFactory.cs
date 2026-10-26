using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GhApp.Support;

namespace GhApp.GitHub;

internal static class JwtFactory
{
    public static string Create(long appId, byte[] privateKeyPem, DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "RS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iat"] = current.AddSeconds(-60).ToUnixTimeSeconds(),
            ["exp"] = current.AddMinutes(9).ToUnixTimeSeconds(),
            ["iss"] = appId,
        };

        var headerPart = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadPart = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = Encoding.ASCII.GetBytes($"{headerPart}.{payloadPart}");

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(Encoding.UTF8.GetString(privateKeyPem));
            var signature = rsa.SignData(
                signingInput,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            );
            return $"{headerPart}.{payloadPart}.{Base64Url(signature)}";
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new GhAppException($"invalid private key: {ex.Message}", ex);
        }
    }

    private static string Base64Url(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
