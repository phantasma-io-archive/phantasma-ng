using Microsoft.AspNetCore.Authentication;

namespace Phantasma.Node.Authentication;

public class BasicAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public string Realm { get; set; }
}
