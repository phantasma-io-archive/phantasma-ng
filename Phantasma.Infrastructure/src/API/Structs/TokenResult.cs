namespace Phantasma.Infrastructure.API;

public class TokenResult
{
    [APIDescription("Ticker symbol for the token")]
    public string symbol { get; set; }

    public string name { get; set; }

    [APIDescription("Amount of decimals when converting from fixed point format to decimal format")]
    public int decimals { get; set; }

    [APIDescription("Amount of minted tokens")]
    public string currentSupply { get; set; }

    [APIDescription("Max amount of tokens that can be minted")]
    public string maxSupply { get; set; }

    [APIDescription("Total amount of burned tokens")]
    public string burnedSupply { get; set; }

    [APIDescription("Address of token contract")]
    public string address { get; set; }

    [APIDescription("Owner address")]
    public string owner { get; set; }

    public string flags { get; set; }

    [APIDescription("Script attached to token, in hex")]
    public string script { get; set; }

    [APIDescription("Series info. NFT only")]
    public TokenSeriesResult[] series { get; set; }

    [APIDescription("External platforms info")]
    public TokenExternalResult[] external { get; set; }

    [APIDescription("Cosmic swap historic data")]
    public TokenPriceResult[] price { get; set; }
}