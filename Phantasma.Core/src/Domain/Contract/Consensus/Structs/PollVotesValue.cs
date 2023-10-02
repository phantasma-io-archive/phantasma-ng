using System.IO;
using System.Numerics;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Domain.Contract.Consensus.Structs;

public struct PollVotesValue : ISerializable
{
    public PollVote Choice;
    public BigInteger NumberOfVotes;

    public PollVotesValue(PollVote choice, BigInteger NumberOfVotes)
    {
        this.Choice = choice;
        this.NumberOfVotes = NumberOfVotes;
    }

    public void SerializeData(BinaryWriter writer)
    {
        Choice.SerializeData(writer);
        writer.WriteBigInteger(NumberOfVotes);
    }

    public void UnserializeData(BinaryReader reader)
    {
        Choice = new PollVote();
        Choice.UnserializeData(reader);
        NumberOfVotes = reader.ReadBigInteger();
    }
}