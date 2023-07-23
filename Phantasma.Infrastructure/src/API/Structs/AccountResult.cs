namespace Phantasma.Infrastructure.API;

public class AccountResult
{
    public string address { get; set; }
    public string name { get; set; }

    [APIDescription("Info about staking if available")]
    public StakeResult stakes { get; set; }

    public string stake { get; set; } //Deprecated
    public string unclaimed { get; set; }//Deprecated

    [APIDescription("Amount of available KCAL for relay channel")]
    public string relay { get; set; }

    [APIDescription("Validator role")]
    public string validator { get; set; }

    [APIDescription("Info about storage if available")]
    public StorageResult storage { get; set; }

    public BalanceResult[] balances { get; set; }

    public string[] txs { get; set; }
}