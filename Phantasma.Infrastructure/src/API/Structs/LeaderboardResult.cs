namespace Phantasma.Infrastructure.API.Structs;

public class LeaderboardResult
{
    public string name { get; set; }
    public LeaderboardRowResult[] rows { get; set; }
}