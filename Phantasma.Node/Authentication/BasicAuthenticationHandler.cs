using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Phantasma.Node.Authentication;

public class BasicAuthenticationHandler : AuthenticationHandler<BasicAuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public BasicAuthenticationHandler(IOptionsMonitor<BasicAuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, ISystemClock clock, IConfiguration configuration) : base(options, logger, encoder, clock)
    {
        _configuration = configuration;
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.TryAdd(HeaderNames.WWWAuthenticate,
            new StringValues($@"Basic realm=""{Options.Realm}"", charset=""UTF-8"""));

        return base.HandleChallengeAsync(properties);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(HeaderNames.Authorization))
            return Task.FromResult(AuthenticateResult.Fail("Missing authorization header"));

        if (!AuthenticationHeaderValue.TryParse(Request.Headers[HeaderNames.Authorization], out var headerValue))
            return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header"));

        if (!BasicAuthenticationDefaults.AuthenticationScheme.Equals(headerValue.Scheme,
                StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Invalid authentication scheme"));

        var openApiConfig = _configuration.GetSection("OpenApi");
        var openApiCredentialStr =
            $"{openApiConfig.GetValue<string>("Username")}:{openApiConfig.GetValue<string>("Password")}";
        var openApiCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes(openApiCredentialStr));

        if (headerValue.Parameter != openApiCredential)
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));

        var identity = new ClaimsIdentity(Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
