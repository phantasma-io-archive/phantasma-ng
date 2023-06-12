using System.IO;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Relay;

public struct RelayMessage : ISerializable
{
    public string nexus;
    public BigInteger index;
    public Timestamp timestamp;
    public Address sender;
    public Address receiver;
    public byte[] script;

    public byte[] ToByteArray()
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                SerializeData(writer);
            }
            return stream.ToArray();
        }
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(nexus);
        writer.WriteBigInteger(index);
        writer.Write(timestamp.Value);
        writer.WriteAddress(sender);
        writer.WriteAddress(receiver);
        writer.WriteByteArray(script);
    }

    public void UnserializeData(BinaryReader reader)
    {
        nexus = reader.ReadVarString();
        index = reader.ReadBigInteger();
        timestamp = new Timestamp(reader.ReadUInt32());
        sender = reader.ReadAddress();
        receiver = reader.ReadAddress();
        script = reader.ReadByteArray();
    }
}
