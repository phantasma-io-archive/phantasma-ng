using System.Collections.Generic;
using Microsoft.OpenApi.Models;

namespace Phantasma.Node.Swagger;

public static class SwaggerExtensions
{
    public static IEnumerable<OpenApiSchema> FindAdditionalSchema(this OpenApiSchema schema,
        IDictionary<string, OpenApiSchema> listOfDefinition)
    {
        if (!string.IsNullOrEmpty(schema?.Reference?.ReferenceV2))
        {
            OpenApiSchema definition;
            if (listOfDefinition.TryGetValue(schema.Reference.Id, out definition))
                foreach (var propertySchema in definition.Properties)
                    yield return propertySchema.Value;
        }

        if (!string.IsNullOrEmpty(schema?.Items?.Reference?.Id))
        {
            OpenApiSchema definition;
            if (listOfDefinition.TryGetValue(schema.Items.Reference.Id, out definition))
                foreach (var propertySchema in definition.Properties)
                    yield return propertySchema.Value;
        }
    }

    public static IEnumerable<OpenApiSchema> EnumerateSchema(this OpenApiSchema schema,
        IDictionary<string, OpenApiSchema> listOfDefinition, int dept = 0)
    {
        if (schema == null) yield break;
        if (dept > 64) yield break;
        if (dept == 0) yield return schema;

        var listOfAdditionalSchema = schema.FindAdditionalSchema(listOfDefinition) ?? new List<OpenApiSchema>();
        foreach (var additionalSchema in listOfAdditionalSchema)
        {
            yield return additionalSchema;
            foreach (var childSchema in additionalSchema.EnumerateSchema(listOfDefinition, dept++) ??
                                        new List<OpenApiSchema>())
                yield return childSchema;
        }
    }
}
