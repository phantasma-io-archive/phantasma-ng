using System.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Stake;

public struct VotingLogEntry
{
    public Timestamp timestamp;
    public BigInteger amount;
}
