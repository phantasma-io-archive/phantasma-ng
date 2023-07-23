using System.Numerics;
using Phantasma.Core.Cryptography.Structs;

namespace Phantasma.Core.Domain.Contract.LeaderboardDetails.Structs;

public struct LeaderboardRow
{
    public Address address;
    public BigInteger score;
}
