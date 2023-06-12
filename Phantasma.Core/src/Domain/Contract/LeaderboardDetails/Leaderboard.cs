using System.Numerics;
using Phantasma.Core.Cryptography;

namespace Phantasma.Core.Domain.Contract.LeaderboardDetails
{
    public struct Leaderboard
    {
        public string name;
        public Address owner;
        public BigInteger size;
        public BigInteger round;
    }

}
