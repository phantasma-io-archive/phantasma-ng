using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Consensus;

public struct PollValue : ISerializable
{
    public byte[] value;
    public BigInteger ranking;
    public BigInteger votes;

    public PollValue(byte[] value, BigInteger ranking, BigInteger votes)
    {
        this.value = value;
        this.ranking = ranking;
        this.votes = votes;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteByteArray(value);
        writer.WriteBigInteger(ranking);
        writer.WriteBigInteger(votes);
    }

    public void UnserializeData(BinaryReader reader)
    {
        value = reader.ReadByteArray();
        ranking = reader.ReadBigInteger();
        votes = reader.ReadBigInteger();
    }
}
