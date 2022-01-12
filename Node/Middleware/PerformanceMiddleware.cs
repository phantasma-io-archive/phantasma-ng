using System.Diagnostics;
using System.Threading.Tasks;
using Phantasma.Spook.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Phantasma.Spook.Middleware;

public class PerformanceMiddleware
{
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly IEndpointMetrics _metrics;
    private readonly RequestDelegate _next;

    public PerformanceMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMiddleware> logger,
        IEndpointMetrics metrics
    )
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task Invoke(
        HttpContext httpContext
    )
    {
        Stopwatch timer = Stopwatch.StartNew();

        await _next(httpContext);

        timer.Stop();

        HttpRequest request = httpContext.Request;

        if (httpContext.Response.StatusCode >= 400 ||
            request.Path.StartsWithSegments("/swagger") ||
            request.Path.StartsWithSegments("/swagger-internal"))
        {
            // Ignore server errors and Swagger endpoints

            return;
        }

        if (Settings.Default.PerformanceMetrics.CountsEnabled)
        {
            await _metrics.Count(request.Path);
        }

        if (Settings.Default.PerformanceMetrics.AveragesEnabled)
        {
            await _metrics.Average(request.Path, timer.ElapsedMilliseconds);
        }

        if (timer.ElapsedMilliseconds <= Settings.Default.PerformanceMetrics.LongRunningRequestThreshold)
        {
            return;
        }

        _logger.LogWarning("Long Running Request: Duration: {Duration}ms; Path: {Path}; Query: {@Query}",
            timer.ElapsedMilliseconds, request.Path, request.Query);
    }
}
