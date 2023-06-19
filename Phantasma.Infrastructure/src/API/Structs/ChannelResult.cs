namespace Phantasma.Infrastructure.API;

public class ChannelResult
{
    [APIDescription("Creator of channel")]
    public string creatorAddress { get; set; }

    [APIDescription("Target of channel")]
    public string targetAddress { get; set; }

    [APIDescription("Name of channel")]
    public string name { get; set; }

    [APIDescription("Chain of channel")]
    public string chain { get; set; }

    [APIDescription("Creation time")]
    public uint creationTime { get; set; }

    [APIDescription("Token symbol")]
    public string symbol { get; set; }

    [APIDescription("Fee of messages")]
    public string fee { get; set; }

    [APIDescription("Estimated balance")]
    public string balance { get; set; }

    [APIDescription("Channel status")]
    public bool active { get; set; }

    [APIDescription("Message index")]
    public int index { get; set; }
}