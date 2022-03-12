using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Phantasma.Node.Authentication;

namespace Phantasma.Node.Swagger;

public class SwaggerAuthorizationMiddleware
{
    private readonly ILogger<SwaggerAuthorizationMiddleware> _logger;
    private readonly RequestDelegate _next;

    public SwaggerAuthorizationMiddleware(RequestDelegate next, ILogger<SwaggerAuthorizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context, IPrincipal principal)
    {
        if (!context.Request.Path.StartsWithSegments("/swagger-internal") &&
            !context.Request.Path.StartsWithSegments("/swagger/v1-internal"))
        {
            await _next.Invoke(context);

            return;
        }

        if (principal.Identity is { IsAuthenticated: true })
        {
            await _next.Invoke(context);

            return;
        }

        var result = await context.AuthenticateAsync(BasicAuthenticationDefaults.AuthenticationScheme);
        if (result.Succeeded)
        {
            await _next.Invoke(context);

            return;
        }

        _logger.LogWarning(result.Failure,
            $"API documentation endpoint unauthorized access attempt by [{context.Connection.RemoteIpAddress}]");
        await context.ChallengeAsync(BasicAuthenticationDefaults.AuthenticationScheme);
    }
}
