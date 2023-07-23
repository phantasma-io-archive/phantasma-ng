using System.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Domain.Contract.Stake;

public struct EnergyStake
{
    public BigInteger stakeAmount;
    public Timestamp stakeTime;
}
