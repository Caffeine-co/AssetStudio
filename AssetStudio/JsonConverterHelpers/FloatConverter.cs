using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public static partial class JsonConverterHelper
    {
        public class FloatConverter : JsonConverter<float>
        {
            public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    return text switch
                    {
                        "NaN" => float.NaN,
                        "Infinity" => float.PositiveInfinity,
                        "-Infinity" => float.NegativeInfinity,
                        _ when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                        _ => throw new JsonException("Expected float string value."),
                    };
                }
                return reader.GetSingle();
            }

            public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    if (options.NumberHandling == JsonNumberHandling.AllowNamedFloatingPointLiterals)
                    {
                        writer.WriteStringValue($"{value.ToString(CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        throw new JsonException("Named floating point literals are not enabled.");
                    }
                }
                else
                {
                    writer.WriteNumberValue((decimal)value + 0.0m);
                }
            }
        }
    }
}
