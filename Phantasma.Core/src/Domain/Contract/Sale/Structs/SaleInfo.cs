using System.Numerics;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract.Sale.Enums;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Domain.Contract.Sale.Structs;

public struct SaleInfo
{
    public Address Creator;
    public string Name;
    public SaleFlags Flags;
    public Timestamp StartDate;
    public Timestamp EndDate;

    public string SellSymbol;
    public string ReceiveSymbol;
    public BigInteger Price;
    public BigInteger GlobalSoftCap;
    public BigInteger GlobalHardCap;
    public BigInteger UserSoftCap;
    public BigInteger UserHardCap;
}
