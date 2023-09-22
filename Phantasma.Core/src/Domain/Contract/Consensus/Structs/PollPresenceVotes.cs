using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Consensus.Structs;

public struct PollPresenceVotes : ISerializable
{
    public string subject;
    public BigInteger round;
    public PollVotesValue[] votes;

    public PollPresenceVotes(string subject, BigInteger round, PollVotesValue[] votes)
    {
        this.subject = subject;
        this.round = round;
        this.votes = votes;
    }

    public void SerializeData(BinaryWriter writer)
    {
        writer.WriteVarString(subject);
        writer.WriteBigInteger(round);
        writer.WriteBigInteger(votes.Length);
        foreach (var vote in votes)
        {
            vote.SerializeData(writer);
        }
    }

    public void UnserializeData(BinaryReader reader)
    {
        subject = reader.ReadVarString();
        round = reader.ReadBigInteger();
        var votesLength = (int)reader.ReadBigInteger();
        votes = new PollVotesValue[votesLength];
        for (int i = 0; i < votesLength; i++)
        {
            votes[i].UnserializeData(reader);
        }
    }
}
