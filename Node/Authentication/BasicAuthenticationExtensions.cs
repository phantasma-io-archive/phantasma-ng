using Microsoft.AspNetCore.Authentication;

namespace Phantasma.Spook.Authentication;

public static class BasicAuthenticationExtensions
{
    public static AuthenticationBuilder AddBasicAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<BasicAuthenticationSchemeOptions, BasicAuthenticationHandler>(
            BasicAuthenticationDefaults.AuthenticationScheme, options => options.Realm = "Phantasma");
    }
}
