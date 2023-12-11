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

    public async Task InvokeAsync(HttpContext context, APIExplorerService explorerService, APIChainService chainService)
    {
        if (context.Request.Path.StartsWithSegments("/Explorer"))
        {
            if (context.Items.ContainsKey("APIService"))
            {
                context.Items["APIService"] = explorerService;
            }
            else
            {
                context.Items.Add("APIService", explorerService);
            }
            
            context.Request.Path = context.Request.Path.Value.Replace("/Explorer", "");
            
        }
        else
        {
            if (context.Items.ContainsKey("APIService"))
            {
                context.Items["APIService"] = chainService;
            }
            else
            {
                context.Items.Add("APIService", chainService);
            }
        }

        await _next(context);
    }
}

