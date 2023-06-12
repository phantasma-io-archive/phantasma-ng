using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Interop;

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
