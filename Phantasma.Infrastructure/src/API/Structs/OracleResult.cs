namespace Phantasma.Infrastructure.API;

public class OracleResult
{
    [APIDescription("URL that was read by the oracle")]
    public string url { get; set; }

    [APIDescription("Byte array content read by the oracle, encoded as hex string")]
    public string content { get; set; }
}