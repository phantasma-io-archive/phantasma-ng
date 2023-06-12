using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Consensus;

public struct PollPresence : ISerializable
{
    public string subject;
    public BigInteger round;

    public PollPresence(string subject, BigInteger round)
    {
        this.subject = subject;
        this.round = round;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(subject);
        writer.WriteBigInteger(round);
    }

    public void UnserializeData(BinaryReader reader)
    {
        subject = reader.ReadVarString();
        round = reader.ReadBigInteger();
    }
}
