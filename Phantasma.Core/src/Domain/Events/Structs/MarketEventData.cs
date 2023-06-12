using System.Numerics;
using Phantasma.Core.Domain.Contract.Market.Enums;

namespace Phantasma.Core.Domain.Events.Structs;

public struct MarketEventData
{
    public string BaseSymbol;
    public string QuoteSymbol;
    public BigInteger ID;
    public BigInteger Price;
    public BigInteger EndPrice;
    public TypeAuction Type;
}
