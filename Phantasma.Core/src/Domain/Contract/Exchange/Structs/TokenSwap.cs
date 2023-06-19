using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Exchange.Structs;

public struct TokenSwap
{
    public Address buyer;
    public Address seller;
    public string baseSymbol;
    public string quoteSymbol;
    public BigInteger value;
    public BigInteger price;
}
