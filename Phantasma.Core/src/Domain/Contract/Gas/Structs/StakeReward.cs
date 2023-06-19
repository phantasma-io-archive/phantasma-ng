using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Core.Domain.Contract.Gas.Structs;

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
