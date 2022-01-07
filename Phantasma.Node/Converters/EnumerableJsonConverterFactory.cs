using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Phantasma.Spook.Converters;

public class EnumerableJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType) return false;

        var realType = typeToConvert.GetGenericTypeDefinition();

        return realType.IsAssignableTo(typeof(IEnumerable<>));
    }

    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
    {
        var valueType = type.GetGenericArguments()[0];

        var converter = (JsonConverter)Activator.CreateInstance(
            typeof(EnumerableJsonConverter<>).MakeGenericType(valueType), BindingFlags.Instance | BindingFlags.Public,
            null, new object[] { options }, null);

        return converter;
    }
}
