namespace Phantasma.Core.Domain.Contract.Sale.Enums;

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
