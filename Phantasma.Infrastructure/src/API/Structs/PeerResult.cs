namespace Phantasma.Infrastructure.API;

public class PeerResult
{
    [APIDescription("URL of peer")]
    public string url { get; set; }

    [APIDescription("Software version of peer")]
    public string version { get; set; }

    [APIDescription("Features supported by peer")]
    public string flags { get; set; }

    [APIDescription("Minimum fee required by node")]
    public string fee { get; set; }

    [APIDescription("Minimum proof of work required by node")]
    public uint pow { get; set; }

    [APIDescription("List of exposed ports")]
    public PortResult[] ports { get; set; }
}