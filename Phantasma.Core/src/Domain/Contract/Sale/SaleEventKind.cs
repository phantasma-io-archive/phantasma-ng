namespace Phantasma.Core.Domain.Contract.Sale;

public enum SaleEventKind
{
    Creation,
    SoftCap,
    HardCap,
    AddedToWhitelist,
    RemovedFromWhitelist,
    Distribution,
    Refund,
    PriceChange,
    Participation,
}
