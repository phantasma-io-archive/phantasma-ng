using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.LeaderboardDetails.Structs
{
    public struct Leaderboard : ISerializable
    {
        public string name;
        public Address owner;
        public BigInteger size;
        public BigInteger round;
        
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(name);
            writer.WriteAddress(owner);
            writer.WriteBigInteger(size);
            writer.WriteBigInteger(round);
        }

        public void UnserializeData(BinaryReader reader)
        {
            name = reader.ReadVarString();
            owner = reader.ReadAddress();
            size = reader.ReadBigInteger();
            round = reader.ReadBigInteger();
        }
    }

}
