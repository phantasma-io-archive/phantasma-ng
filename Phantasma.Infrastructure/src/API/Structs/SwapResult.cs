namespace Phantasma.Infrastructure.API;

public class SwapResult
{
    public string sourcePlatform { get; set; }
    public string sourceChain { get; set; }
    public string sourceHash { get; set; }
    public string sourceAddress { get; set; }

    public string destinationPlatform { get; set; }
    public string destinationChain { get; set; }
    public string destinationHash { get; set; }
    public string destinationAddress { get; set; }

    public string symbol { get; set; }
    public string value { get; set; }
}