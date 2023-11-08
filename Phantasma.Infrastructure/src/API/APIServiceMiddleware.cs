using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Phantasma.Infrastructure.API.Interfaces;

namespace Phantasma.Infrastructure.API;

public class APIServiceMiddleware
{
    private readonly RequestDelegate _next;

    public APIServiceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAPIService explorerService, IAPIService chainService)
    {
        if (context.Request.Path.StartsWithSegments("/Explorer"))
        {
            context.Items["APIService"] = explorerService;
        }
        else
        {
            context.Items["APIService"] = chainService;
        }

        await _next(context);
    }
}

