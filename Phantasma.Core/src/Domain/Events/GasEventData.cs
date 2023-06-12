using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Events;

public struct GasEventData
{
    public readonly Address address;
    public readonly BigInteger price;
    public readonly BigInteger amount;

    public GasEventData(Address address, BigInteger price, BigInteger amount)
    {
        this.address = address;
        this.price = price;
        this.amount = amount;
    }
}
