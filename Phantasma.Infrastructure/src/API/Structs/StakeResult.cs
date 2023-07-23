namespace Phantasma.Infrastructure.API;

public class StakeResult
{
    [APIDescription("Amount of staked SOUL")]
    public string amount { get; set; }

    [APIDescription("Time of last stake")]
    public uint time { get; set; }

    [APIDescription("Amount of claimable KCAL")]
    public string unclaimed { get; set; }
}