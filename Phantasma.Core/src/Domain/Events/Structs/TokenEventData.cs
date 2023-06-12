using System.Numerics;

namespace Phantasma.Core.Domain.Events.Structs;

public struct TokenEventData
{
    public readonly string Symbol;
    public readonly BigInteger Value;
    public readonly string ChainName;

    public TokenEventData(string symbol, BigInteger value, string chainName)
    {
        this.Symbol = symbol;
        this.Value = value;
        this.ChainName = chainName;
    }
}
