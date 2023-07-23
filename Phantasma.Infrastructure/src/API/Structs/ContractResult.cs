namespace Phantasma.Infrastructure.API;

public class ContractResult
{
    [APIDescription("Name of contract")]
    public string name { get; set; }

    [APIDescription("Address of contract")]
    public string address { get; set; }

    [APIDescription("Script bytes, in hex format")]
    public string script { get; set; }

    [APIDescription("List of methods")]
    public ABIMethodResult[] methods { get; set; }

    [APIDescription("List of events")]
    public ABIEventResult[] events { get; set; }
}