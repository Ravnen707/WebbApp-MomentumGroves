using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MomentumGroves.Helper;

/// <summary>
/// Reads numeric JSON values and numeric strings with either decimal separator.
/// </summary>
public sealed class FlexibleDecimalJsonConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString()?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                var separator = value.Contains(',') ? ',' : '.';
                if (separator == ',' && value.Contains('.'))
                {
                    throw new JsonException("Mixed decimal separators are not supported.");
                }

                var normalized = separator == ',' ? value.Replace(',', '.') : value;
                if (double.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed))
                {
                    return parsed;
                }
            }
        }

        throw new JsonException("Expected a valid numeric value.");
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}