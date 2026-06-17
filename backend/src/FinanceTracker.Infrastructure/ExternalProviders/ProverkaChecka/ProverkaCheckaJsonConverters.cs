using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceTracker.Infrastructure.ExternalProviders.ProverkaChecka;

/// <summary>
/// Reads a JSON value as a string regardless of how the provider typed it. The
/// ПроверкаЧека API is loosely typed and returns id-like fields (fiscal sign /
/// drive number, INN) sometimes as quoted strings and sometimes as bare numbers;
/// a plain <c>string</c> property would throw on the numeric form. Booleans are
/// accepted defensively too. Numbers are rendered with the invariant culture so a
/// long fiscal sign is preserved exactly.
/// </summary>
internal sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when reading a string."),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>
/// Deserializes an object property that the provider occasionally returns as an
/// empty array (<c>[]</c>) or a scalar instead of an object/<c>null</c> when there
/// is no payload — e.g. <c>data</c> on a non-success <c>code</c>. Any token that is
/// not an object is treated as "absent" (<c>null</c>) rather than throwing, so the
/// business <c>code</c> is never lost to a wrapper-shape mismatch.
/// </summary>
/// <remarks>
/// Recursion-safe: the <c>[JsonConverter]</c> attribute is applied on the
/// <em>property</em>, not on <typeparamref name="T"/>, so deserializing
/// <typeparamref name="T"/> directly does not re-enter this converter.
/// </remarks>
internal sealed class TolerantObjectConverter<T> : JsonConverter<T> where T : class
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.StartObject:
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    return doc.RootElement.Deserialize<T>(options);
                }
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
