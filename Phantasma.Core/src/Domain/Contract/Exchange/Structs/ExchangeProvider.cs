using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.Exchange.Structs;

public struct ExchangeProvider
{
    public static readonly ExchangeProvider Null = new ExchangeProvider
    {
        address = Address.Null
    };

    public Address address;
    public string id;
    public string name;
    public BigInteger TotalFeePercent;
    public BigInteger FeePercentForExchange;
    public BigInteger FeePercentForPool;
    public Hash dapp;
}
