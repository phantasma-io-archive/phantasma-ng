namespace Phantasma.Infrastructure.API;

public class CrowdsaleResult
{
    public string hash { get; set; }
    public string name { get; set; }
    public string creator { get; set; }
    public string flags { get; set; }
    public uint startDate { get; set; }
    public uint endDate { get; set; }
    public string sellSymbol { get; set; }
    public string receiveSymbol { get; set; }
    public uint price { get; set; }
    public string globalSoftCap { get; set; }
    public string globalHardCap { get; set; }
    public string userSoftCap { get; set; }
    public string userHardCap { get; set; }
}