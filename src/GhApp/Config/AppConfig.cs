using System.Text.Json.Serialization;

namespace GhApp.Config;

internal sealed class AppConfig
{
    [JsonPropertyName("appId")]
    [JsonConverter(typeof(FlexibleIdConverter))]
    public long AppId { get; set; }

    [JsonPropertyName("installationId")]
    [JsonConverter(typeof(FlexibleIdConverter))]
    public long InstallationId { get; set; }

    [JsonPropertyName("organization")]
    public string? Organization { get; set; }

    [JsonPropertyName("privateKeyPath")]
    public string? PrivateKeyPath { get; set; }

    // Alias accepted for compatibility with older config formats.
    [JsonPropertyName("privateKeyFile")]
    public string? PrivateKeyFile { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonIgnore]
    public string? EffectivePrivateKeyPath => PrivateKeyPath ?? PrivateKeyFile;
}
