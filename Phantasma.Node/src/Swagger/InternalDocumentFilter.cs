using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Phantasma.Infrastructure.API;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Phantasma.Node.Swagger;

public class InternalDocumentFilter : IDocumentFilter
{
    private readonly IConfiguration _configuration;

    public InternalDocumentFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var isInternal = context.DocumentName.EndsWith("-internal");
        var config = _configuration.GetSection("OpenApi");
        var showPublicEndpointsInInternalDocs = config.GetValue<bool>("ShowPublicEndpointsInInternalDocs");

        foreach (var description in context.ApiDescriptions)
        {
            var attribute = description.ActionDescriptor.EndpointMetadata.OfType<APIInfoAttribute>().FirstOrDefault();
            if (description.RelativePath == null || description.HttpMethod == null) continue;

            switch (isInternal)
            {
                case true when attribute is { InternalEndpoint: true }:
                case true when showPublicEndpointsInInternalDocs && attribute is { InternalEndpoint: false }:
                case false when attribute is not { InternalEndpoint: true }:
                    continue;
            }

            var key = "/" + description.RelativePath.TrimEnd('/');
            var operation = (OperationType)Enum.Parse(typeof(OperationType), description.HttpMethod, true);

            swaggerDoc.Paths[key].Operations.Remove(operation);

            // Drop the entire route of there are no operations left
            if (!swaggerDoc.Paths[key].Operations.Any()) swaggerDoc.Paths.Remove(key);

            var operations = swaggerDoc.Paths.SelectMany(p => p.Value.Operations.Values).ToArray();

            var responses = operations.SelectMany(o => o.Responses.Values)
                .SelectMany(r => r.Content.Values)
                .Select(c => c.Schema)
                .SelectMany(x => x.EnumerateSchema(swaggerDoc.Components.Schemas))
                .ToArray();

            var requests = operations.Where(o => o.RequestBody != null)
                .SelectMany(o => o.RequestBody.Content.Values)
                .Select(c => c.Schema)
                .SelectMany(x => x.EnumerateSchema(swaggerDoc.Components.Schemas))
                .ToArray();

            var referenceSchema = new List<OpenApiSchema>(responses);
            referenceSchema.AddRange(requests);

            var list1 = referenceSchema.Where(s => s.Reference != null).Select(s => s.Reference.Id).ToList();
            var list2 = referenceSchema.Where(s => s.Items?.Reference != null).Select(s => s.Items.Reference.Id)
                .ToList();
            var list3 = list1.Concat(list2).Distinct().ToArray();

            var listOfUnreferencedDefinition = swaggerDoc.Components.Schemas
                .Where(x => list3.All(y => y != x.Key)).ToList();

            foreach (var unreferencedDefinition in listOfUnreferencedDefinition)
                swaggerDoc.Components.Schemas.Remove(unreferencedDefinition.Key);
        }
    }
}
