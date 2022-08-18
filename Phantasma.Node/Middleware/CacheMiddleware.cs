using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Phantasma.Infrastructure;
using Phantasma.Node.Caching;

namespace Phantasma.Node.Middleware;

public class CacheMiddleware
{
    private readonly ILogger<CacheMiddleware> _logger;
    private readonly RequestDelegate _next;

    public CacheMiddleware(RequestDelegate next, ILogger<CacheMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext httpContext, IEndpointCacheManager cacheManager)
    {
        var endpoint = httpContext.Features.Get<IEndpointFeature>()?.Endpoint;
        if (endpoint == null)
        {
            await _next(httpContext);

            return;
        }

        if (endpoint.Metadata.FirstOrDefault(m => m is APIInfoAttribute) is not APIInfoAttribute apiInfoMeta)
        {
            await _next(httpContext);

            return;
        }

        var routeKey = endpoint.DisplayName;
        if (endpoint is RouteEndpoint routeEndpoint)
            // Use RoutePattern if available or fallback to display name
            routeKey = routeEndpoint.RoutePattern.RawText ?? endpoint.DisplayName;

        if (string.IsNullOrEmpty(routeKey))
        {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Could not apply cache to endpoint due to empty route key");

            await _next(httpContext);

            return;
        }

        // Set cache-control header
        //httpContext.Response.Headers.CacheControl = apiInfoMeta.CacheDuration switch
        //{
        //    < 0 when !apiInfoMeta.InternalEndpoint => "public, max-age=3600",
        //    > 0 when !apiInfoMeta.InternalEndpoint => $"public, max-age={apiInfoMeta.CacheDuration}",
        //    _ => httpContext.Response.Headers.CacheControl
        //};

        var cacheResult = await cacheManager.Get(routeKey, httpContext.Request.Query, apiInfoMeta.CacheTag);

        if (cacheResult.Cached)
        {
            var response = Encoding.UTF8.GetBytes(cacheResult.Content);
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            await httpContext.Response.Body.WriteAsync(response);

            return;
        }

        var originalBodyStream = httpContext.Response.Body;

        try
        {
            using var memoryBodyStream = new MemoryStream();
            httpContext.Response.Body = memoryBodyStream;

            await _next(httpContext);

            memoryBodyStream.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(memoryBodyStream).ReadToEndAsync();
            memoryBodyStream.Seek(0, SeekOrigin.Begin);

            await memoryBodyStream.CopyToAsync(originalBodyStream);
            await cacheManager.Add(cacheResult.Key, body, apiInfoMeta.CacheDuration, apiInfoMeta.CacheTag);
        }
        finally
        {
            httpContext.Response.Body = originalBodyStream;
        }
    }
}
