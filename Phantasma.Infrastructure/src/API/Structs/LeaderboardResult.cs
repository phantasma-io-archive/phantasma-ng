namespace Phantasma.Infrastructure.API;

public class LeaderboardResult
{
    public string name { get; set; }
    public LeaderboardRowResult[] rows { get; set; }
}