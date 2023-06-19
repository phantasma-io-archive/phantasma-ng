using System;
using System.IO;
using System.Linq;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.EdDSA;
using Phantasma.Core.Domain.Interfaces;

namespace Phantasma.Core.Domain.Contract.Relay.Structs;

public struct RelayReceipt : ISerializable
{
    public RelayMessage message;
    public Signature signature;

    public void SerializeData(BinaryWriter writer)
    {
        message.SerializeData(writer);
        writer.WriteSignature(signature);
    }

    public void UnserializeData(BinaryReader reader)
    {
        message.UnserializeData(reader);
        signature = reader.ReadSignature();
    }

    public static RelayReceipt FromBytes(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        {
            using (var reader = new BinaryReader(stream))
            {
                var receipt = new RelayReceipt();
                receipt.UnserializeData(reader);
                return receipt;
            }
        }
    }

    public static RelayReceipt FromMessage(RelayMessage msg, PhantasmaKeys keys)
    {
        if (msg.script == null || msg.script.SequenceEqual(new byte[0]))
            throw new Exception("RelayMessage script cannot be empty or null");

        var bytes = msg.ToByteArray();
        var signature = Ed25519Signature.Generate(keys, bytes);
        return new RelayReceipt()
        {
            message = msg,
            signature = signature
        };
    }
}
