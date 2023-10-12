namespace Phantasma.Infrastructure.API.Structs;

public class ABIEventResult
{
    [APIDescription("Value of event")]
    public int value { get; set; }

    [APIDescription("Name of event")]
    public string name { get; set; }

    public string returnType { get; set; }

    [APIDescription("Description script (base16 encoded)")]
    public string description { get; set; }
}