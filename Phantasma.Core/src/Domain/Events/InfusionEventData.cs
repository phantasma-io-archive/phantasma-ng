using System.Numerics;

namespace Phantasma.Core.Domain.Events;

public struct InfusionEventData
{
    public readonly string BaseSymbol;
    public readonly BigInteger TokenID;
    public readonly string InfusedSymbol;
    public readonly BigInteger InfusedValue;
    public readonly string ChainName;

    public InfusionEventData(string baseSymbol, BigInteger tokenID, string infusedSymbol, BigInteger infusedValue, string chainName)
    {
        BaseSymbol = baseSymbol;
        TokenID = tokenID;
        InfusedSymbol = infusedSymbol;
        InfusedValue = infusedValue;
        ChainName = chainName;
    }
}
