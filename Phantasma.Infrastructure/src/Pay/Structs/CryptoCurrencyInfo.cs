namespace Phantasma.Infrastructure.Pay;

public struct CryptoCurrencyInfo
{
    public readonly string Symbol;
    public readonly string Name;
    public readonly int Decimals;
    public readonly string Platform;
    public readonly CryptoCurrencyCaps Caps;

    public CryptoCurrencyInfo(string symbol, string name, int decimals, string platform, CryptoCurrencyCaps caps)
    {
        Symbol = symbol;
        Name = name;
        Decimals = decimals;
        Platform = platform;
        Caps = caps;
    }
}
