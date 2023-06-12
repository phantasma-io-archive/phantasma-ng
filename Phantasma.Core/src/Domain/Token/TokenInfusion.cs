using System.Numerics;

namespace Phantasma.Core.Domain.Token;

public struct TokenInfusion
{
    public readonly string Symbol;
    public readonly BigInteger Value;

    public TokenInfusion(string symbol, BigInteger value)
    {
        Symbol = symbol;
        Value = value;
    }
}
