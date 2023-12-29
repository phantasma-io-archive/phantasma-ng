using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.LeaderboardDetails.Structs;

public struct LeaderboardRow : ISerializable
{
    public Address address;
    public BigInteger score;
    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteAddress(address);
        writer.WriteBigInteger(score);
    }

    public void UnserializeData(BinaryReader reader)
    {
        address = reader.ReadAddress();
        score = reader.ReadBigInteger();
    }
}
