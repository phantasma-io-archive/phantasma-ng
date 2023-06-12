namespace Phantasma.Core.Domain.Contract.Exchange;

public enum ExchangeOrderType
{
    OTC,
    Limit, //normal limit order
    ImmediateOrCancel, //special limit order, any unfulfilled part of the order gets cancelled if not immediately fulfilled
    Market, //normal market order
    //TODO: FillOrKill = 4,         //Either gets 100% fulfillment or it gets cancelled , no partial fulfillments like in IoC order types
}
