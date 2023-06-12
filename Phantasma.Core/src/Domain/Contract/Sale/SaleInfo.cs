using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Sale;

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
