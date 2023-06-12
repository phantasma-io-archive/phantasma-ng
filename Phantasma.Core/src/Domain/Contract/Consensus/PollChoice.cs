using System.IO;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Consensus;

public struct PollChoice : ISerializable
{
    public byte[] value;

    public PollChoice(byte[] value)
    {
        this.value = value;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteByteArray(value);
    }

    public void UnserializeData(BinaryReader reader)
    {
        value = reader.ReadByteArray();
    }
}
