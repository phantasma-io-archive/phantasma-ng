using System.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Stake;

public struct EnergyStake
{
    public BigInteger stakeAmount;
    public Timestamp stakeTime;
}
