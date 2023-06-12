namespace Phantasma.Infrastructure.API;

public class SignatureResult
{
    [APIDescription("Kind of signature")]
    public string Kind { get; set; }

    [APIDescription("Byte array containing signature data, encoded as hex string")]
    public string Data { get; set; }
}