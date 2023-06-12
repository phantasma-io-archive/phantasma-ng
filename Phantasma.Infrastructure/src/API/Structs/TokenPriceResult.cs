namespace Phantasma.Infrastructure.API;

public class TokenPriceResult
{
    public uint Timestamp { get; set; }
    public string Open { get; set; }
    public string High { get; set; }
    public string Low { get; set; }
    public string Close { get; set; }
}