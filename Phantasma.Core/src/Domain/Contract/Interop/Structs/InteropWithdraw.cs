using System.Numerics;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Domain.Contract.Interop.Structs;

public struct InteropWithdraw
{
    public Hash hash;
    public Address destination;
    public string transferSymbol;
    public BigInteger transferAmount;
    public string feeSymbol;
    public BigInteger feeAmount;
    public Timestamp timestamp;
}
