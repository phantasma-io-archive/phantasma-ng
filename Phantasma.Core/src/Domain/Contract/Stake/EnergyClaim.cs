using System.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Stake;

public struct EnergyClaim
{
    public BigInteger stakeAmount;
    public Timestamp claimDate;
    public bool isNew;
}
