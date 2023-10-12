namespace Phantasma.Infrastructure.API.Structs;

public class ABIParameterResult
{
    [APIDescription("Name of method")]
    public string name { get; set; }

    public string type { get; set; }
}