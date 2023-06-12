using System.Text.Json;
using System.Text.Json.Nodes;

namespace Phantasma.Core.Domain
{
    public static class APIUtils
    {
        public static JsonNode FromAPIResult(object input)
        {
            return FromObject(input);
        }

        private static JsonNode FromObject(object input)
        {
            return JsonNode.Parse(JsonSerializer.Serialize(input, input.GetType()));
        }
    }

}
