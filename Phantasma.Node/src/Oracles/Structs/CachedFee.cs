using System.Numerics;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Node.Oracles.Structs;

public struct CachedFee
{
    public Timestamp Time;
    public BigInteger Value;

    public CachedFee(Timestamp time, BigInteger value)
    {
        this.Time = time;
        this.Value = value;
    }
}
