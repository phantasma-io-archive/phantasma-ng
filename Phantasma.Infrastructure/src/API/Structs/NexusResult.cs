namespace Phantasma.Infrastructure.API.Structs;

public class NexusResult
{
    [APIDescription("Name of the nexus")]
    public string name { get; set; }

    [APIDescription("Network protocol version")]
    public uint protocol { get; set; }

    [APIDescription("List of platforms")]
    public PlatformResult[] platforms { get; set; }

    [APIDescription("List of tokens")]
    public TokenResult[] tokens { get; set; }

    [APIDescription("List of chains")]
    public ChainResult[] chains { get; set; }

    [APIDescription("List of governance values")]
    public GovernanceResult[] governance { get; set; }

    [APIDescription("List of organizations")]
    public string[] organizations { get; set; }
}