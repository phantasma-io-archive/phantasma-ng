namespace Phantasma.Infrastructure.API;

public class ChainResult
{
    public string name { get; set; }
    public string address { get; set; }

    [APIDescription("Name of parent chain")]
    public string parent { get; set; }

    [APIDescription("Current chain height")]
    public uint height { get; set; }

    [APIDescription("Chain organization")]
    public string organization { get; set; }

    [APIDescription("Contracts deployed in the chain")]
    public string[] contracts { get; set; }

    [APIDescription("Dapps deployed in the chain")]
    public string[] dapps { get; set; }
}