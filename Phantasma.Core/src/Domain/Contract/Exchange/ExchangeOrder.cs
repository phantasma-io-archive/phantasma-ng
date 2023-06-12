using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Exchange;

public struct ExchangeOrder
{
    public readonly BigInteger Uid;
    public readonly Timestamp Timestamp;
    public readonly Address Creator;
    public readonly Address Provider;

    public readonly BigInteger Amount;
    public readonly string BaseSymbol;

    public readonly BigInteger Price;
    public readonly string QuoteSymbol;

    public readonly ExchangeOrderSide Side;
    public readonly ExchangeOrderType Type;

    public ExchangeOrder(BigInteger uid, Timestamp timestamp, Address creator, Address provider, BigInteger amount,
        string baseSymbol, BigInteger price, string quoteSymbol, ExchangeOrderSide side, ExchangeOrderType type)
    {
        Uid = uid;
        Timestamp = timestamp;
        Creator = creator;
        Provider = provider;

        Amount = amount;
        BaseSymbol = baseSymbol;

        Price = price;
        QuoteSymbol = quoteSymbol;

        Side = side;
        Type = type;
    }

    public ExchangeOrder(ExchangeOrder order, BigInteger newPrice, BigInteger newOrderSize)
    {
        Uid = order.Uid;
        Timestamp = order.Timestamp;
        Creator = order.Creator;
        Provider = order.Provider;

        Amount = newOrderSize;
        BaseSymbol = order.BaseSymbol;

        Price = newPrice;
        QuoteSymbol = order.QuoteSymbol;

        Side = order.Side;
        Type = order.Type;
    }
}
