namespace Phantasma.Infrastructure.API.Structs;

public class TokenDataResult
{
    [APIDescription("id of token")]
    public string ID { get; set; }

    [APIDescription("series id of token")]
    public string series { get; set; }

    [APIDescription("mint number of token")]
    public string mint { get; set; }

    [APIDescription("Chain where currently is stored")]
    public string chainName { get; set; }

    [APIDescription("Address who currently owns the token")]
    public string ownerAddress { get; set; }

    [APIDescription("Address who minted the token")]
    public string creatorAddress { get; set; }

    [APIDescription("Writable data of token, hex encoded")]
    public string ram { get; set; }

    [APIDescription("Read-only data of token, hex encoded")]
    public string rom { get; set; }

    [APIDescription("Status of nft")]
    public string status { get; set; }

    public TokenPropertyResult[] infusion { get; set; }

    public TokenPropertyResult[] properties { get; set; }
}