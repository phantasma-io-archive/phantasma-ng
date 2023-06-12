using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.Exchange;

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
