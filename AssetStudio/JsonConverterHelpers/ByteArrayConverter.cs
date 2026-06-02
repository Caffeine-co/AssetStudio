using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public static partial class JsonConverterHelper
    {
        public class ByteArrayConverter : JsonConverter<byte[]>
        {
            public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    return reader.GetBytesFromBase64();
                }
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new JsonException("Expected byte array or base64 string.");
                }

                var bytes = new List<byte>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        return bytes.ToArray();
                    }
                    if (!reader.TryGetByte(out var value))
                    {
                        throw new JsonException("Expected byte value.");
                    }
                    bytes.Add(value);
                }
                throw new JsonException("Unterminated byte array.");
            }

            public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            {
                writer.WriteBase64StringValue(value);
            }
        }
    }
}
