namespace Phantasma.Infrastructure.API.Structs;

public class SendRawTxResult
{
    [APIDescription("Transaction hash")]
    public string hash { get; set; }

    [APIDescription("Error message if transaction did not succeed")]
    public string error { get; set; }
}