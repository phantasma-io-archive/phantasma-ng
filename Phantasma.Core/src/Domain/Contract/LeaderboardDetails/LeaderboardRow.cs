using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.LeaderboardDetails;

public struct LeaderboardRow
{
    public Address address;
    public BigInteger score;
}
