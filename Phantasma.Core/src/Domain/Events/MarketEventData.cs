using System.Numerics;
using Phantasma.Core.Domain.Contract.Market;

namespace Phantasma.Core.Domain.Events;

public struct MarketEventData
{
    public string BaseSymbol;
    public string QuoteSymbol;
    public BigInteger ID;
    public BigInteger Price;
    public BigInteger EndPrice;
    public TypeAuction Type;
}
