using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.Exchange;

public struct TokenSwap
{
    public Address buyer;
    public Address seller;
    public string baseSymbol;
    public string quoteSymbol;
    public BigInteger value;
    public BigInteger price;
}
