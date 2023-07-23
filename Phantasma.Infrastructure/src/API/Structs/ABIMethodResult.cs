namespace Phantasma.Infrastructure.API;

public class ABIMethodResult
{
    [APIDescription("Name of method")]
    public string name { get; set; }

    public string returnType { get; set; }

    [APIDescription("Type of parameters")]
    public ABIParameterResult[] parameters { get; set; }
}