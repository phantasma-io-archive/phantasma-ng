using System.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Domain.Contract.Stake;

public struct VotingLogEntry
{
    public Timestamp timestamp;
    public BigInteger amount;
}
