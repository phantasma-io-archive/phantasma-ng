using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Phantasma.Node.Converters;

public class EnumerableJsonConverter<TValue> : JsonConverter<IEnumerable<TValue>>
{
    private readonly JsonConverter<TValue> _valueConverter;
    private readonly Type _valueType;

    public EnumerableJsonConverter(JsonSerializerOptions options)
    {
        // For performance, use the existing converter if available
        _valueConverter = (JsonConverter<TValue>)options.GetConverter(typeof(TValue));

        // Cache the key and value types
        _valueType = typeof(TValue);
    }

    public override IEnumerable<TValue> Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException();

        var list = new List<TValue>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return list;

            // Get the value
            var value = _valueConverter.Read(ref reader, _valueType, options) ??
                        JsonSerializer.Deserialize<TValue>(ref reader, options);

            // Add to list
            list.Add(value);
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable<TValue> values, JsonSerializerOptions options)
    {
        var enumerable = values as TValue[] ?? values.ToArray();
        if (!enumerable.Any())
        {
            writer.WriteNullValue();

            return;
        }

        writer.WriteStartArray();

        foreach (var value in enumerable)
            if (_valueConverter != null)
                _valueConverter.Write(writer, value, options);
            else
                JsonSerializer.Serialize(writer, value, options);

        writer.WriteEndArray();
    }
}
