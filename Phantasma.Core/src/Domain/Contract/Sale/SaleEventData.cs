using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.Sale;

public struct SaleEventData
{
    public Hash saleHash;
    public SaleEventKind kind;
}
