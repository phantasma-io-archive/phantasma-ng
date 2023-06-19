namespace Phantasma.Infrastructure.API;

public class ABIParameterResult
{
    [APIDescription("Name of method")]
    public string name { get; set; }

    public string type { get; set; }
}