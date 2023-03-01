using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.ECDsa;
using Phantasma.Core.Cryptography.EdDSA;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public enum ConsensusMode
    {
        Unanimity,
        Majority,
        Popularity,
        Ranking,
    }

    public enum PollState
    {
        Inactive,
        Active,
        Consensus,
        Failure,
        Finished
    }

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

    public sealed class ConsensusContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Consensus;

#pragma warning disable 0649
        internal StorageMap _pollMap; //<string, Poll> 
        internal StorageList _pollList;
        internal StorageMap _presences; // address, List<PollPresence>
        internal StorageMap _transactionMap; // string, Transaction
        internal StorageMap _transactionMapRules; // string, List<Address>
        internal StorageMap _transactionMapSigned; // string, Transaction
#pragma warning restore 0649

        public const int MinimumPollLength = 86400;
        public const string MaximumPollLengthTag = "poll.max.length";
        public const string MaxEntriesPerPollTag = "poll.max.entries";
        public const string PollVoteLimitTag = "poll.vote.limit";
        public static readonly BigInteger PollVoteLimitDefault = 50000;
        public static readonly BigInteger MaxEntriesPerPollDefault = 10;
        public static readonly BigInteger MaximumPollLengthDefault = MinimumPollLength * 90;
        public static readonly uint DefaultConsensusTime = 1296000; // 15 Days (timestamp)

        public const string SystemPoll = "system.";

        public ConsensusContract() : base()
        {
        }

        public void Migrate(Address from, Address target)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == NativeContractKind.Account.GetContractName(), "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            _presences.Migrate<Address, StorageList>(from, target);
        }

        private ConsensusPoll FetchPoll(string subject)
        {
            var poll = _pollMap.Get<string, ConsensusPoll>(subject);

            if (!Runtime.IsReadOnlyMode())
            {
                var MaxVotesPerPoll = Runtime.GetGovernanceValue(PollVoteLimitTag);

                if (Runtime.Time < poll.startTime && poll.state != PollState.Inactive)
                {
                    poll.state = PollState.Inactive;
                }
                else if (Runtime.Time >= poll.startTime && Runtime.Time < poll.endTime && poll.state == PollState.Inactive)
                {
                    poll.state = PollState.Active;
                    _pollList.Add(subject);
                }
                else if ((Runtime.Time >= poll.endTime || poll.totalVotes >= MaxVotesPerPoll) && poll.state == PollState.Active)
                {
                    // its time to count votes...
                    BigInteger totalVotes = 0;
                    for (int i = 0; i < poll.entries.Length; i++)
                    {
                        var entry = poll.entries[i];
                        totalVotes += entry.votes;
                    }

                    if (totalVotes == 0) return poll;

                    var rankings = poll.entries.OrderByDescending(x => x.votes).ToArray();

                    var winner = rankings[0];

                    bool hasTies = rankings.Length > 1 && rankings[1].votes == winner.votes;

                    for (int i = 0; i < poll.entries.Length; i++)
                    {
                        var val = poll.entries[i].value;
                        int index = -1;
                        for (int j = 0; j < rankings.Length; j++)
                        {
                            if (rankings[j].value == val)
                            {
                                index = j;
                                break;
                            }
                        }
                        Runtime.Expect(index >= 0, "missing entry in poll rankings");

                        poll.entries[i].ranking = index;
                    }

                    BigInteger percentage = winner.votes * 100 / totalVotes;

                    if (poll.mode == ConsensusMode.Unanimity && percentage < 100)
                    {
                        poll.state = PollState.Failure;
                    }
                    else if (poll.mode == ConsensusMode.Majority && percentage < 51)
                    {
                        poll.state = PollState.Failure;
                    }
                    else if (poll.mode == ConsensusMode.Popularity && hasTies)
                    {
                        poll.state = PollState.Failure;
                    }
                    else
                    {
                        poll.state = PollState.Consensus;
                    }

                    _pollMap.Set(subject, poll);

                    Runtime.Notify(EventKind.PollClosed, Address, subject);
                }
                else
                {
                    if (Runtime.ProtocolVersion >= 8)
                    {
                        if (Runtime.Time >= poll.endTime.Value + DefaultConsensusTime && poll.state == PollState.Consensus)
                        {
                            poll.state = PollState.Finished;
                            _pollMap.Set(subject, poll);
                        }
                    }
                }

            }

            return poll;
        }

        public void InitPollV2(Address from, string subject, string organization, ConsensusMode mode, Timestamp startTime, Timestamp endTime, byte[] serializedChoices, BigInteger votesPerUser, Timestamp consensusTime)
        {
            Runtime.Expect(Runtime.OrganizationExists(organization), "invalid organization");

            // TODO support for passing structs as args
            var choices = Serialization.Unserialize<PollChoice[]>(serializedChoices);

            if (subject.ToLower().StartsWith(SystemPoll))
            {
                Runtime.Expect(Runtime.IsPrimaryValidator(from), "must be validator");

                if (subject.ToLower().StartsWith(SystemPoll + "stake."))
                {
                    Runtime.Expect(organization == DomainSettings.MastersOrganizationName, "must require votes from masters");
                }

                Runtime.Expect(mode == ConsensusMode.Majority, "must use majority mode for system governance");
            }

            Runtime.Expect(Runtime.IsRootChain(), "not root chain");

            Runtime.Expect(organization == DomainSettings.ValidatorsOrganizationName, "community polls not yet");

            var maxEntriesPerPoll = Runtime.GetGovernanceValue(MaxEntriesPerPollTag);
            Runtime.Expect(choices.Length > 1, "invalid amount of entries");
            Runtime.Expect(choices.Length <= maxEntriesPerPoll, "too many entries");

            var MaximumPollLength = (uint)Runtime.GetGovernanceValue(MaximumPollLengthTag);

            Runtime.Expect(startTime >= Runtime.Time, "invalid start time");
            var minEndTime = new Timestamp(startTime.Value + MinimumPollLength);
            var maxEndTime = new Timestamp(startTime.Value + MaximumPollLength);
            Runtime.Expect(endTime >= minEndTime, "invalid end time");
            Runtime.Expect(endTime <= maxEndTime, "invalid end time");

            Runtime.Expect(votesPerUser > 0, "number of votes per user too low");
            Runtime.Expect(votesPerUser < choices.Length, "number of votes per user too high");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            ConsensusPoll poll;
            if (_pollMap.ContainsKey(subject))
            {
                poll = FetchPoll(subject);
                if (Runtime.ProtocolVersion <= 8)
                {
                    Runtime.Expect(poll.state == PollState.Consensus || poll.state == PollState.Failure, "poll already in progress");
                }
                else
                {
                    Runtime.Expect(poll.state == PollState.Consensus || poll.state == PollState.Failure || poll.state == PollState.Finished, "poll already in progress");
                }

                poll.round += 1;
                poll.state = PollState.Inactive;
            }
            else
            {
                poll = new ConsensusPoll();
                poll.subject = subject;
                poll.round = 1;
            }

            poll.startTime = startTime;
            poll.endTime = endTime;
            poll.organization = organization;
            poll.mode = mode;
            poll.state = PollState.Inactive;
            poll.choicesPerUser = votesPerUser;
            poll.totalVotes = 0;
            if (Runtime.ProtocolVersion >= 8)
            {
                poll.consensusTime = consensusTime;
            }

            var electionName = SystemPoll + ValidatorContract.ValidatorPollTag;
            if (subject == electionName)
            {
                for (int i = 0; i < choices.Length; i++)
                {
                    Runtime.Expect(choices[i].value.Length == Address.LengthInBytes, "election choices must be public addresses");
                    var address = Address.FromBytes(choices[i].value);
                    Runtime.Expect(Runtime.IsKnownValidator(address), "election choice must be active or waiting validator");
                }
            }

            poll.entries = new PollValue[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                poll.entries[i] = new PollValue()
                {
                    ranking = -1,
                    value = choices[i].value,
                    votes = 0
                };
            }

            _pollMap.Set(subject, poll);

            Runtime.Notify(EventKind.PollCreated, Address, subject);
        }

        public void InitPoll(Address from, string subject, string organization, ConsensusMode mode, Timestamp startTime,
            Timestamp endTime, byte[] serializedChoices, BigInteger votesPerUser)
        {
            InitPollV2(from, subject, organization, mode, startTime, endTime, serializedChoices, votesPerUser, DefaultConsensusTime);
        }

        public void SingleVote(Address from, string subject, BigInteger index)
        {
            MultiVote(from, subject, new PollVote[] { new PollVote() { index = index, percentage = 100 } });
        }

        public void MultiVote(Address from, string subject, PollVote[] choices)
        {
            Runtime.Expect(_pollMap.ContainsKey(subject), "invalid poll subject");

            Runtime.Expect(choices.Length > 0, "invalid number of choices");

            var poll = FetchPoll(subject);

            Runtime.Expect(poll.state == PollState.Active, "poll not active");

            var organization = Runtime.GetOrganization(poll.organization);
            Runtime.Expect(organization.IsMember(from), "must be member of organization: " + poll.organization);

            Runtime.Expect(choices.Length <= poll.choicesPerUser, "too many choices");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var presences = _presences.Get<Address, StorageList>(from);
            var count = presences.Count();
            int index = -1;
            BigInteger round = 0;

            for (int i = 0; i < count; i++)
            {
                var presence = presences.Get<PollPresence>(i);
                if (presence.subject == subject)
                {
                    index = -1;
                    round = presence.round;
                    break;
                }
            }

            if (index >= 0)
            {
                Runtime.Expect(round < poll.round, "already voted");
            }

            BigInteger votingPower;

            if (poll.organization == DomainSettings.StakersOrganizationName)
            {
                votingPower = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), from).AsNumber();
            }
            else
            {
                votingPower = 100;
            }

            Runtime.Expect(votingPower > 0, "not enough voting power");

            for (int i = 0; i < choices.Length; i++)
            {
                var votes = votingPower * choices[i].percentage / 100;
                Runtime.Expect(votes > 0, "choice percentage is too low");

                var targetIndex = (int)choices[i].index;
                poll.entries[targetIndex].votes += votes;
            }

            poll.totalVotes += 1;
            _pollMap.Set(subject, poll);

            // finally add this voting round to the presences list
            var temp = new PollPresence()
            {
                subject = subject,
                round = poll.round,
            };

            if (index >= 0)
            {
                presences.Replace(index, temp);
            }
            else
            {
                presences.Add(temp);
            }

            Runtime.Notify(EventKind.PollVote, from, subject);
        }

        public bool HasConsensus(string subject, byte[] value)
        {
            if (subject.StartsWith(SystemPoll))
            {
                if (Runtime.ProtocolVersion < DomainSettings.Phantasma30Protocol)
                {
                    var validatorCount = Runtime.GetPrimaryValidatorCount();
                    if (validatorCount <= 1)
                    {
                        return false;
                    }
                }
                else
                {
                    var validatorCount = Runtime.InvokeContractAtTimestamp(NativeContractKind.Validator,
                        nameof(ValidatorContract.GetMaxTotalValidators)).AsNumber();
                    if (validatorCount <= 1)
                    {
                        return false;
                    }
                }

            }

            var rank = GetRank(subject, value);
            return rank == 0;
        }

        public BigInteger GetRank(string subject, byte[] value)
        {
            Runtime.Expect(_pollMap.ContainsKey(subject), "invalid poll subject");

            var poll = FetchPoll(subject);
            Runtime.Expect(poll.state == PollState.Consensus, "no consensus reached");

            for (int i = 0; i < poll.entries.Length; i++)
            {
                if (poll.entries[i].value.SequenceEqual(value))
                {
                    return poll.entries[i].ranking;
                }
            }

            Runtime.Expect(_pollMap.ContainsKey(subject), "invalid value");
            return -1;
        }

        public ConsensusPoll GetConsensusPoll(string subject)
        {
            return _pollMap.Get<string, ConsensusPoll>(subject);
        }

        public ConsensusPoll[] GetConsensusPolls()
        {
            return _pollMap.AllValues<ConsensusPoll>();

            /*
            var count = _pollList.Count();
            var result = new ConsensusPoll[(int)count];
            var pollList = _pollList.All<string>();
            int index = 0;
            foreach(var poll in pollList)
            {
                result[index] = _pollMap.Get<string, ConsensusPoll>(poll);
                index++;
            }
            
            return result;*/
        }

        #region Multisignature Transactions
        /// <summary>
        /// Gets the multisignature transaction.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <returns></returns>
        public Transaction GetTransaction(Address from, string subject)
        {
            Runtime.Expect(Runtime.IsWitness(from), "not a valid witness");
            Runtime.Expect(_transactionMap.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(_transactionMapRules.ContainsKey(subject), "transaction doesn't exist");
            var transaction = _transactionMapSigned.Get<string, Transaction>(subject);
            var addresses = _transactionMapRules.Get<string, Address[]>(subject);
            Runtime.Expect(addresses.Contains(from), "not a valid witness for the transaction");
            return transaction;
        }

        /// <summary>
        /// Creates a transaction to be signed by multiple parties.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="transaction"></param>
        public void CreateTransaction(Address from, string subject, Transaction transaction, Address[] listOfUsers)
        {
            Runtime.Expect(Runtime.IsWitness(from), "not a valid witness");
            Runtime.Expect(!_transactionMap.ContainsKey(subject), "transaction already exists");
            Runtime.Expect(!_transactionMapSigned.ContainsKey(subject), "transaction already exists");

            _transactionMap.Set(subject, transaction);
            _transactionMapSigned.Set(subject, transaction);
            _transactionMapRules.Set(subject, listOfUsers);
        }

        /// <summary>
        /// Signs a transaction / Adds a signature to a transaction
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="signature">Should be Ed25519</param>
        public void AddSignatureTransaction(Address from, string subject, byte[] signature)
        {
            Runtime.Expect(Runtime.IsWitness(from), "not a valid witness");
            Runtime.Expect(_transactionMap.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(_transactionMapSigned.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(_transactionMapRules.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(signature != null, "null signature");
            Runtime.Expect(signature.Length != 0, "invalid signature length");

            var transaction = _transactionMapSigned.Get<string, Transaction>(subject);
            var addresses = _transactionMapRules.Get<string, Address[]>(subject);
            if (signature.Length == 65)
                signature = signature.Skip(1).ToArray();
            Signature sig = new Ed25519Signature(signature);

            Runtime.Expect(addresses.Contains(from), "not a valid witness for the transaction");
            Runtime.Expect(!transaction.Signatures.Contains(sig), "User already signed the transaction");

            var msg = transaction.ToByteArray(false);
            Runtime.Expect(sig.Verify(msg, from), "invalid signature");
            transaction.AddSignature(sig);
            _transactionMapSigned.Set(subject, transaction);
        }

        /// <summary>
        /// Deletes a transaction from the list of transactions to be signed
        /// </summary>
        /// <param name="addresses"></param>
        /// <param name="subject"></param>
        public void DeleteTransaction(Address[] addresses, string subject)
        {
            Runtime.Expect(addresses.Length > 0, "invalid from");
            Runtime.Expect(subject != null, "invalid subject");
            Runtime.Expect(subject.Length > 0, "invalid subject");
            Runtime.Expect(_transactionMap.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(_transactionMapSigned.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(_transactionMapRules.ContainsKey(subject), "transaction doesn't exist");
            var transactionAddresses = _transactionMapRules.Get<string, Address[]>(subject);
            bool isWitness = false;
            foreach (var address in addresses)
            {
                Runtime.Expect(transactionAddresses.Contains(address), "not a valid witness for the transaction");
                if (Runtime.IsWitness(address))
                {
                    isWitness = true;
                }
            }

            Runtime.Expect(isWitness, "not a valid witness");
            _transactionMap.Remove(subject);
            _transactionMapSigned.Remove(subject);
            _transactionMapRules.Remove(subject);
        }

        /// <summary>
        /// To execute the transaction with multiple signatures
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /*public void ExecuteTransaction(Address from, string subject)
        {
            Runtime.Expect(Runtime.IsWitness(from), "not a valid witness");
            var transaction = _transactionMapSigned.Get<string, Transaction>(subject);
            //Runtime.Chain.Nexus.
            //Runtime.Chain.AddBlock();
        }*/
        #endregion
    }
}
