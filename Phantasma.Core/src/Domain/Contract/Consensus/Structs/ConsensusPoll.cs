using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Contract.Consensus.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Types.Structs;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Consensus.Structs;

public struct ConsensusPoll : ISerializable
{
    public string subject;
    public string organization;
    public ConsensusMode mode;
    public PollState state;
    public PollValue[] entries;
    public BigInteger round;
    public Timestamp startTime;
    public Timestamp endTime;
    public BigInteger choicesPerUser;
    public BigInteger totalVotes;
    public Timestamp consensusTime;

    public ConsensusPoll(string subject, string organization, ConsensusMode mode, PollState state, PollValue[] entries, BigInteger round, Timestamp startTime, Timestamp endTime, BigInteger choicesPerUser, BigInteger totalVotes, Timestamp consensusTime)
    {
        this.subject = subject;
        this.organization = organization;
        this.mode = mode;
        this.state = state;
        this.entries = entries;
        this.round = round;
        this.startTime = startTime;
        this.endTime = endTime;
        this.choicesPerUser = choicesPerUser;
        this.totalVotes = totalVotes;
        this.consensusTime = consensusTime;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(subject);
        writer.WriteVarString(organization);
        writer.Write((byte)mode);
        writer.Write((byte)state);
        writer.Write(entries.Length);
        foreach (var entry in entries)
        {
            entry.SerializeData(writer);
        }
        writer.WriteBigInteger(round);
        writer.WriteTimestamp(startTime);
        writer.WriteTimestamp(endTime);
        writer.WriteBigInteger(choicesPerUser);
        writer.WriteBigInteger(totalVotes);
        writer.WriteTimestamp(consensusTime);
    }

    public void UnserializeData(BinaryReader reader)
    {
        subject = reader.ReadVarString();
        organization = reader.ReadVarString();
        mode = (ConsensusMode)reader.ReadByte();
        state = (PollState)reader.ReadByte();
        var count = reader.ReadInt32();
        entries = new PollValue[count];
        for (int i = 0; i < count; i++)
        {
            entries[i].UnserializeData(reader);
        }
        round = reader.ReadBigInteger();
        startTime = reader.ReadTimestamp();
        endTime = reader.ReadTimestamp();
        choicesPerUser = reader.ReadBigInteger();
        totalVotes = reader.ReadBigInteger();
        consensusTime = reader.ReadTimestamp();
    }
}
