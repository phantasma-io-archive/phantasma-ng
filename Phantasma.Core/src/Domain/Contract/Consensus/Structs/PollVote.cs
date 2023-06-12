using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Consensus.Structs;

public struct PollVote : ISerializable
{
    public BigInteger index;
    public BigInteger percentage;

    public PollVote(BigInteger index, BigInteger percentage)
    {
        this.index = index;
        this.percentage = percentage;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteBigInteger(index);
        writer.WriteBigInteger(percentage);
    }

    public void UnserializeData(BinaryReader reader)
    {
        index = reader.ReadBigInteger();
        percentage = reader.ReadBigInteger();
    }
}
