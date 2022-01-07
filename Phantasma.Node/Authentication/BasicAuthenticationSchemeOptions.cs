using Microsoft.AspNetCore.Authentication;

namespace Phantasma.Spook.Authentication;

public class BasicAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string Realm { get; set; }
}
