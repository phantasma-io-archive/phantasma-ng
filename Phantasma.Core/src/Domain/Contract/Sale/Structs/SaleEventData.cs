using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract.Sale.Enums;

namespace Phantasma.Core.Domain.Contract.Sale.Structs;

public struct SaleEventData
{
    public Hash saleHash;
    public SaleEventKind kind;
}
