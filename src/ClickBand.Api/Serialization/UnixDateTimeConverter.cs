using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClickBand.Api.Serialization;

public sealed class UnixDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var seconds = reader.GetInt64();
            return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        if (reader.TokenType == JsonTokenType.String &&
            DateTime.TryParse(reader.GetString(), out var result))
        {
            return result.ToUniversalTime();
        }

        throw new JsonException("Unsupported date format.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var seconds = new DateTimeOffset(value.ToUniversalTime()).ToUnixTimeSeconds();
        writer.WriteNumberValue(seconds);
    }
}

public sealed class UnixDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var seconds = reader.GetInt64();
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        if (reader.TokenType == JsonTokenType.String &&
            DateTimeOffset.TryParse(reader.GetString(), out var result))
        {
            return result.ToUniversalTime();
        }

        throw new JsonException("Unsupported date format.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTimeSeconds());
    }
}
