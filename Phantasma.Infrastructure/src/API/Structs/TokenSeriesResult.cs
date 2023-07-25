namespace Phantasma.Infrastructure.API.Structs;

public class TokenSeriesResult
{
    public uint seriesID { get; set; }

    [APIDescription("Current amount of tokens in circulation")]
    public string currentSupply { get; set; }

    [APIDescription("Maximum possible amount of tokens")]
    public string maxSupply { get; set; }

    [APIDescription("Total amount of burned tokens")]
    public string burnedSupply { get; set; }

    public string mode { get; set; }

    public string script { get; set; }

    [APIDescription("List of methods")]
    public ABIMethodResult[] methods { get; set; }
}