namespace Phantasma.Infrastructure.API.Structs;

public class AuctionResult
{
    [APIDescription("Address of auction creator")]
    public string creatorAddress { get; set; }

    [APIDescription("Address of auction chain")]
    public string chainAddress { get; set; }
    public uint startDate { get; set; }
    public uint endDate { get; set; }
    public string baseSymbol { get; set; }
    public string quoteSymbol { get; set; }
    public string tokenId { get; set; }
    public string price { get; set; }
    public string endPrice { get; set; }
    public string extensionPeriod { get; set; }
    public string type { get; set; }
    public string rom { get; set; }
    public string ram { get; set; }
    public string listingFee { get; set; }
    public string currentWinner { get; set; }
}