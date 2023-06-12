using Phantasma.Core.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.Core.Domain.Contract.Gas;

public struct StakeReward
{
    public readonly Address staker;
    public readonly Timestamp date;

    public StakeReward(Address staker, Timestamp date)
    {
        this.staker = staker;
        this.date = date;
    }
}
