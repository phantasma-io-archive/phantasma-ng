namespace Phantasma.Infrastructure.API.Structs;

public class PortResult
{
    [APIDescription("Port description")]
    public string name { get; set; }

    [APIDescription("Port number")]
    public int port { get; set; }
}