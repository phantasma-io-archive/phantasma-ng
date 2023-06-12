namespace Phantasma.Infrastructure.Pay;

public struct WalletBalance
{
    public readonly string Symbol;
    public readonly decimal Amount;
    public readonly string Chain;

    public WalletBalance(string symbol, decimal amount, string chain = "main")
    {
        Symbol = symbol;
        Amount = amount;
        Chain = chain;
    }
}
