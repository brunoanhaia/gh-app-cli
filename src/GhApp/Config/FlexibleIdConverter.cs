using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GhApp.Config;

// Accepts IDs as a JSON number or string.
internal sealed class FlexibleIdConverter : JsonConverter<long>
{
    public override long Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return 0;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var number))
                {
                    return Validate(number);
                }
                throw new JsonException("ID must be an integer");
            case JsonTokenType.String:
                var raw = reader.GetString()?.Trim() ?? "";
                if (raw.Length == 0)
                {
                    return 0;
                }
                if (
                    !long.TryParse(
                        raw,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var parsed
                    )
                )
                {
                    throw new JsonException($"invalid ID: {raw}");
                }
                return Validate(parsed);
            default:
                throw new JsonException("ID must be a number or string");
        }
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }

    private static long Validate(long value)
    {
        if (value < 0)
        {
            throw new JsonException("ID cannot be negative");
        }
        return value;
    }
}
