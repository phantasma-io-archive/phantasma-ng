using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.ECDsa;
using Phantasma.Core.Cryptography.EdDSA;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Consensus;
using Phantasma.Core.Domain.Contract.Consensus.Enums;
using Phantasma.Core.Domain.Contract.Consensus.Structs;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class ConsensusContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Consensus;

#pragma warning disable 0649
        internal StorageMap _pollMap; //<string, Poll> 
        internal StorageList _pollList; // (Deprecated - Can't remove)
        internal StorageMap _presences; // address, List<PollPresence> (Deprecated)
        internal StorageMap _pollVotesPerAddress; // address, map<string, List<PollPresenceVotes>>
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

        /// <summary>
        /// Migrate the contract to a new address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="target"></param>
        public void Migrate(Address from, Address target)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == NativeContractKind.Account.GetContractName(),
                "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            _presences.Migrate<Address, StorageList>(from, target);
        }


        /// <summary>
        /// Fetch Poll To Support the old version
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        private ConsensusPoll FetchPoll(string subject)
        {
            if (Runtime.ProtocolVersion < 17)
                return FetchPollV1(subject);
            else
                return FetchPollV2(subject);
        }

        /// <summary>
        /// Fetch a poll by subject
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        private ConsensusPoll FetchPollV1(string subject)
        {
            var poll = _pollMap.Get<string, ConsensusPoll>(subject);

            if (!Runtime.IsReadOnlyMode())
            {
                var MaxVotesPerPoll = Runtime.GetGovernanceValue(PollVoteLimitTag);

                if (Runtime.Time < poll.startTime && poll.state != PollState.Inactive)
                {
                    poll.state = PollState.Inactive;
                }
                else if (Runtime.Time >= poll.startTime && Runtime.Time < poll.endTime &&
                         poll.state == PollState.Inactive)
                {
                    poll.state = PollState.Active;
                    _pollList.Add(subject);
                }
                else if ((Runtime.Time >= poll.endTime || poll.totalVotes >= MaxVotesPerPoll) &&
                         poll.state == PollState.Active)
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
                        if (Runtime.Time >= poll.endTime.Value + DefaultConsensusTime &&
                            poll.state == PollState.Consensus)
                        {
                            poll.state = PollState.Finished;
                            _pollMap.Set(subject, poll);
                        }
                    }
                }
            }

            return poll;
        }

        /// <summary>
        /// Fetch Poll V2 
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        private ConsensusPoll FetchPollV2(string subject)
        {
            var poll = _pollMap.Get<string, ConsensusPoll>(subject);

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
            else if ((Runtime.Time >= poll.endTime || poll.totalVotes >= MaxVotesPerPoll) &&
                     poll.state == PollState.Active)
            {
                // its time to count votes...
                poll.state = GetConsensusPollResult(poll);
                _pollMap.Set(subject, poll);

                Runtime.Notify(EventKind.PollClosed, Address, subject);
            }
            else if (Runtime.Time >= poll.endTime.Value + poll.consensusTime.Value && poll.state == PollState.Consensus)
            {
                poll.state = PollState.Finished;
                _pollMap.Set(subject, poll);

                Runtime.Notify(EventKind.PollClosed, Address, subject);
            }

            return poll;
        }

        /// <summary>
        /// Get the consensus poll result
        /// </summary>
        /// <param name="poll"></param>
        private PollState GetConsensusPollResult(ConsensusPoll poll)
        {
            BigInteger totalVotes = 0;
            for (int i = 0; i < poll.entries.Length; i++)
            {
                var entry = poll.entries[i];
                totalVotes += entry.votes;
            }

            if (totalVotes == 0)
            {
                return PollState.Failure;
            }

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
                return PollState.Failure;
            }
            else if (poll.mode == ConsensusMode.Majority && percentage < 51)
            {
                return PollState.Failure;
            }
            else if (poll.mode == ConsensusMode.Popularity && hasTies)
            {
                return PollState.Failure;
            }

            return PollState.Consensus;
        }

        /// <summary>
        /// Create a new poll (New version with the consensus time)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="organization"></param>
        /// <param name="mode"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <param name="serializedChoices"></param>
        /// <param name="votesPerUser"></param>
        /// <param name="consensusTime"></param>
        public void InitPollV2(Address from, string subject, string organization, ConsensusMode mode,
            Timestamp startTime, Timestamp endTime, byte[] serializedChoices, BigInteger votesPerUser,
            Timestamp consensusTime)
        {
            Runtime.Expect(Runtime.OrganizationExists(organization), "invalid organization");

            var choices = Serialization.Unserialize<PollChoice[]>(serializedChoices);

            if (subject.ToLower().StartsWith(SystemPoll))
            {
                Runtime.Expect(Runtime.IsPrimaryValidator(from), "must be validator");

                if (subject.ToLower().StartsWith(SystemPoll + "stake."))
                {
                    Runtime.Expect(organization == DomainSettings.MastersOrganizationName,
                        "must require votes from masters");
                }

                Runtime.Expect(mode == ConsensusMode.Majority, "must use majority mode for system governance");
            }

            Runtime.Expect(Runtime.IsRootChain(), "not root chain");

            // You should be a member of DAO to create a poll
            if (Runtime.ProtocolVersion >= 17)
            {
                var org = Runtime.GetOrganization(organization);
                Runtime.Expect(org.IsMember(from), "must be member of organization: " + organization);
            }


            if (Runtime.ProtocolVersion <= 13)
            {
                Runtime.Expect(organization == DomainSettings.ValidatorsOrganizationName, "community polls not yet");
            }

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
                    Runtime.Expect(poll.state == PollState.Consensus || poll.state == PollState.Failure,
                        "poll already in progress");
                }
                else
                {
                    Runtime.Expect(
                        poll.state == PollState.Consensus || poll.state == PollState.Failure ||
                        poll.state == PollState.Finished, "poll already in progress");
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
                    Runtime.Expect(choices[i].value.Length == Address.LengthInBytes,
                        "election choices must be public addresses");
                    var address = Address.FromBytes(choices[i].value);
                    Runtime.Expect(Runtime.IsKnownValidator(address),
                        "election choice must be active or waiting validator");
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

        /// <summary>
        /// Initialize a new poll
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="organization"></param>
        /// <param name="mode"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <param name="serializedChoices"></param>
        /// <param name="votesPerUser"></param>
        public void InitPoll(Address from, string subject, string organization, ConsensusMode mode, Timestamp startTime,
            Timestamp endTime, byte[] serializedChoices, BigInteger votesPerUser)
        {
            InitPollV2(from, subject, organization, mode, startTime, endTime, serializedChoices, votesPerUser,
                DefaultConsensusTime);
        }

        /// <summary>
        /// Vote for a single choice
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="index"></param>
        public void SingleVote(Address from, string subject, BigInteger index)
        {
            MultiVote(from, subject, new PollVote[] { new PollVote() { index = index, percentage = 100 } });
        }

        /// <summary>
        /// Vote for multiple choices.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="choices"></param>
        public void MultiVote(Address from, string subject, PollVote[] choices)
        {
            if (Runtime.ProtocolVersion < 17)
                MultiVoteV1(from, subject, choices);
            else
                MultiVoteV2(from, subject, choices);
        }

        /// <summary>
        /// Vote for multiple choices
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="choices"></param>
        private void MultiVoteV1(Address from, string subject, PollVote[] choices)
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
            var index = -1;
            BigInteger round = 0;

            HasAlreadyVotedDepracted(poll, subject, presences);

            BigInteger votingPower;

            if (poll.organization == DomainSettings.StakersOrganizationName ||
                poll.organization == DomainSettings.MastersOrganizationName)
            {
                votingPower = Runtime
                    .CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), from)
                    .AsNumber();
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

        /// <summary>
        /// Vote for multiple choices (New version with the consensus time)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <param name="choices"></param>
        private void MultiVoteV2(Address from, string subject, PollVote[] choices)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(_pollMap.ContainsKey(subject), "invalid poll subject");

            Runtime.Expect(choices.Length > 0, "invalid number of choices");

            var poll = FetchPoll(subject);
            Runtime.Expect(poll.state == PollState.Active, "poll not active");

            var organization = Runtime.GetOrganization(poll.organization);
            Runtime.Expect(organization.IsMember(from), "must be member of organization: " + poll.organization);

            Runtime.Expect(choices.Length <= poll.choicesPerUser, "too many choices");

            var presencesMap = _pollVotesPerAddress.Get<Address, StorageMap>(from);
            var presences = presencesMap.Get<string, StorageList>(subject);

            BigInteger votingPower;
            if (poll.organization == DomainSettings.StakersOrganizationName ||
                poll.organization == DomainSettings.MastersOrganizationName)
            {
                votingPower = Runtime
                    .CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), from)
                    .AsNumber();
            }
            else
            {
                votingPower = 100;
            }

            Runtime.Expect(votingPower > 0, "not enough voting power");

            bool hasAlreadyVoted = HasAlreadyVoted(poll, subject, presences);

            if (hasAlreadyVoted)
                UpdateVotes(from, poll, subject, ref presences, ref presencesMap, choices, votingPower);
            else
                AddNewVotes(from, poll, subject, ref presences, ref presencesMap, choices, votingPower);

            Runtime.Notify(EventKind.PollVote, from, subject);
        }

        /// <summary>
        /// Add the address votes in a poll
        /// </summary>
        /// <param name="poll"></param>
        /// <param name="subject"></param>
        /// <param name="presences"></param>
        /// <param name="index"></param>
        /// <param name="round"></param>
        private void AddNewVotes(Address from, ConsensusPoll poll, string subject, ref StorageList presences,
            ref StorageMap presencesMap, PollVote[] choices, BigInteger votingPower)
        {
            PollVotesValue[] votesValues = new PollVotesValue[choices.Length];

            BigInteger choicePercentageAccumulation = 0;
            Dictionary<BigInteger, bool> choiceIndexMap = new Dictionary<BigInteger, bool>();
            for (int i = 0; i < choices.Length; i++)
            {
                Runtime.Expect(choices[i].percentage > 0 && choices[i].percentage < 101,
                    "choice percentage needs to be between 1 and 100");
                choicePercentageAccumulation += choices[i].percentage;

                Runtime.Expect(choices[i].index >= 0 && choices[i].index < poll.entries.Length,
                    "choice index is invalid");

                Runtime.Expect(!choiceIndexMap.ContainsKey(choices[i].index), "Can't have the same choice index");
                choiceIndexMap.Add(choices[i].index, true);

                var votes = votingPower * choices[i].percentage / 100;
                Runtime.Expect(votes > 0, "choice percentage is too low");
                votesValues[i] = new PollVotesValue()
                {
                    Choice = choices[i],
                    NumberOfVotes = votes
                };

                var targetIndex = (int)choices[i].index;
                poll.entries[targetIndex].votes += votes;
            }

            Runtime.Expect(choicePercentageAccumulation == 100, "choice percentage is too low or too high");

            PollPresenceVotes presenceVotes = new PollPresenceVotes(subject, poll.round, votesValues);

            // Update Poll Number of Votes
            poll.totalVotes += 1;
            _pollMap.Set(subject, poll);

            // Update Address presences
            presences.Add(presenceVotes);
            presencesMap.Set(subject, presences);
            _pollVotesPerAddress.Set<Address, StorageMap>(from, presencesMap);
        }

        /// <summary>
        /// Update the Address votes in a poll
        /// </summary>
        /// <param name="poll"></param>
        /// <param name="subject"></param>
        /// <param name="presences"></param>
        private void UpdateVotes(Address from, ConsensusPoll poll, string subject, ref StorageList presences,
            ref StorageMap presencesMap, PollVote[] choices, BigInteger votingPower)
        {
            // First get the presence
            BigInteger lastPosition = presences.Count() - 1;
            PollPresenceVotes presenceVotes = presences.Get<PollPresenceVotes>(lastPosition);

            if (presenceVotes.round != poll.round)
            {
                return;
            }

            // Remove the user votes from the poll
            for (int i = 0; i < presenceVotes.votes.Length; i++)
            {
                var targetIndex = (int)presenceVotes.votes[i].Choice.index;
                poll.entries[targetIndex].votes -= presenceVotes.votes[i].NumberOfVotes;
            }

            // Add new ones
            BigInteger choicePercentageAccumulation = 0;
            Dictionary<BigInteger, bool> choiceIndexMap = new Dictionary<BigInteger, bool>();

            for (int i = 0; i < choices.Length; i++)
            {
                Runtime.Expect(choices[i].percentage > 0 && choices[i].percentage < 101,
                    "choice percentage needs to be between 1 and 100");
                
                Runtime.Expect(choices[i].index >= 0 && choices[i].index < poll.entries.Length,
                    "choice index is invalid");
                
                Runtime.Expect(!choiceIndexMap.ContainsKey(choices[i].index), "Can't have the same choice index");
                choiceIndexMap.Add(choices[i].index, true);
                
                var votes = votingPower * choices[i].percentage / 100;
                choicePercentageAccumulation += choices[i].percentage;
                Runtime.Expect(votes > 0, "choice percentage is too low");
                presenceVotes.votes[i] = new PollVotesValue()
                {
                    Choice = choices[i],
                    NumberOfVotes = votes
                };

                var targetIndex = (int)choices[i].index;
                poll.entries[targetIndex].votes += votes;
            }
            
            Runtime.Expect(choicePercentageAccumulation == 100,
                $"choice percentage is too low or too high, it needs add up to 100, your value {choicePercentageAccumulation}");

            // Update Poll Entries
            _pollMap.Set(subject, poll);

            // Update Address presences
            presences.Replace(lastPosition, presenceVotes);
            presencesMap.Set(subject, presences);
            _pollVotesPerAddress.Set<Address, StorageMap>(from, presencesMap);
        }

        /// <summary>
        /// Remove votes from the address in a poll
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        public void RemoveVotes(Address from, string subject)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(_pollMap.ContainsKey(subject), "invalid poll subject");

            var poll = FetchPoll(subject);
            Runtime.Expect(poll.state == PollState.Active, "poll not active");

            var organization = Runtime.GetOrganization(poll.organization);
            Runtime.Expect(organization.IsMember(from), "must be member of organization: " + poll.organization);

            Runtime.Expect(_pollVotesPerAddress.ContainsKey<Address>(from), "the address is not in the poll");
            var presencesMap = _pollVotesPerAddress.Get<Address, StorageMap>(from);

            Runtime.Expect(presencesMap.ContainsKey<string>(subject), "the address is not in the poll");
            var presences = presencesMap.Get<string, StorageList>(subject);

            BigInteger lastPosition = presences.Count() - 1;
            PollPresenceVotes presenceVotes = presences.Get<PollPresenceVotes>(lastPosition);

            Runtime.Expect(presenceVotes.round == poll.round, "Address didn't vote on the last round.");

            // Remove the user votes from the poll
            for (int i = 0; i < presenceVotes.votes.Length; i++)
            {
                var targetIndex = (int)presenceVotes.votes[i].Choice.index;
                poll.entries[targetIndex].votes -= presenceVotes.votes[i].NumberOfVotes;
            }

            // Update Poll Number of Votes
            poll.totalVotes -= 1;
            _pollMap.Set(subject, poll);

            // Update Address presences
            presences.RemoveAt(lastPosition);
            presencesMap.Set(subject, presences);
        }

        /// <summary>
        /// Depracted method to check if an address has already voted in a poll
        /// </summary>
        /// <param name="poll"></param>
        /// <param name="subject"></param>
        /// <param name="presences"></param>
        private void HasAlreadyVotedDepracted(ConsensusPoll poll, string subject, StorageList presences)
        {
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
        }

        /// <summary>
        /// Return If the Address has already voted in a poll
        /// </summary>
        /// <param name="poll"></param>
        /// <param name="subject"></param>
        /// <param name="presences"></param>
        /// <returns></returns>
        private bool HasAlreadyVoted(ConsensusPoll poll, string subject, StorageList presences)
        {
            BigInteger count = presences.Count();
            if (count <= 0)
            {
                return false;
            }

            return presences.Get<PollPresenceVotes>(count - 1).round == poll.round;
        }

        /// <summary>
        /// Check if a value has consensus in a poll
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Get the rank of a value in a poll
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Get a ConsensusPoll by subject
        /// </summary>
        /// <param name="subject"></param>
        /// <returns></returns>
        public ConsensusPoll GetConsensusPoll(string subject)
        {
            return _pollMap.Get<string, ConsensusPoll>(subject);
        }

        /// <summary>
        /// Get all ConsensusPolls
        /// </summary>
        /// <returns></returns>
        public ConsensusPoll[] GetConsensusPolls()
        {
            return _pollMap.AllValues<ConsensusPoll>();
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
            if (Runtime.ProtocolVersion <= 13)
            {
                Runtime.Expect(Runtime.IsWitness(from), "not a valid witness");
            }

            Runtime.Expect(_transactionMap.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(_transactionMapRules.ContainsKey(subject), "transaction doesn't exist");
            var transaction = _transactionMapSigned.Get<string, Transaction>(subject);
            var addresses = _transactionMapRules.Get<string, Address[]>(subject);
            Runtime.Expect(addresses.Contains(from), "not a valid witness for the transaction");
            return transaction;
        }

        /// <summary>
        /// Gets the Addresses for a transaction.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="subject"></param>
        /// <returns></returns>
        public Address[] GetAddressesForTransaction(Address from, string subject)
        {
            Runtime.Expect(_transactionMap.ContainsKey(subject), "transaction doesn't exist");
            Runtime.Expect(_transactionMapRules.ContainsKey(subject), "transaction doesn't exist");
            var transaction = _transactionMapSigned.Get<string, Transaction>(subject);
            var addresses = _transactionMapRules.Get<string, Address[]>(subject);
            return addresses;
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
