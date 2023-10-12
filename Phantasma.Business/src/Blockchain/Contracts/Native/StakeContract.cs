using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Org.BouncyCastle.Asn1.X509;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Stake;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class StakeContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Stake;

#pragma warning disable 0649
        private StorageMap _stakeMap; // <Address, EnergyStake>
        private StorageMap _claimMap; // <Address, List<EnergyClaim>>
        private StorageMap _leftoverMap; // <Address, BigInteger>
        private StorageMap _masterAgeMap; // <Address, Timestamp>
        private StorageMap _masterClaims; // <Address, Timestamp>
        private StorageMap _voteHistory; // <Address, List<StakeLog>>
#pragma warning restore 0649

        private Timestamp _lastMasterClaim;

        private BigInteger _currentEnergyRatioDivisor;

        public static readonly BigInteger DefaultMasterThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
        public static readonly BigInteger MasterClaimGlobalAmount = UnitConversion.ToBigInteger(125000, DomainSettings.StakingTokenDecimals);
        public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        public const string MasterStakeThresholdTag = "stake.master.threshold";
        public const string VotingStakeThresholdTag = "stake.vote.threshold";
        public static readonly BigInteger VotingStakeThresholdDefault = UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals);

        public const string StakeSingleBonusPercentTag = "stake.bonus.percent";
        public static readonly BigInteger StakeSingleBonusPercentDefault = 5;

        public const string StakeMaxBonusPercentTag = "stake.bonus.max";
        public static readonly BigInteger StakeMaxBonusPercentDefault = 100;

        public static readonly BigInteger MaxVotingPowerBonus = 1000;
        public static readonly BigInteger DailyVotingBonus = 1;

        public const uint DefaultEnergyRatioDivisor = 500; // used as 1/500, will initially generate 0.002 per staked token

        private static readonly string DesiredPreviousContext = "account";

        public StakeContract() : base()
        {
        }

        /// <summary>
        /// Initializes the contract
        /// </summary>
        /// <param name="from"></param>
        public void Initialize(Address from)
        {
            if (Runtime.ProtocolVersion <= 14)
            {
                _currentEnergyRatioDivisor = DefaultEnergyRatioDivisor; // used as 1/500, will initially generate 0.002 per staked token
                return;
            }
            
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            if ( Runtime.HasGenesis )
                Runtime.Expect(Runtime.IsKnownValidator(from), "invalid validator");
            _currentEnergyRatioDivisor = DefaultEnergyRatioDivisor; // used as 1/500, will initially generate 0.002 per staked token
        }

        /// <summary>
        /// Returns the Master Threshold amount
        /// </summary>
        /// <returns></returns>
        public BigInteger GetMasterThreshold()
        {
            if (Runtime.HasGenesis)
            {
                var amount = Runtime.GetGovernanceValue(MasterStakeThresholdTag);
                return amount;
            }

            return DefaultMasterThreshold;
        }

        /// <summary>
        /// Returns if the given address is a master
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool IsMaster(Address address)
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            return masters.IsMember(address);
        }

        /// <summary>
        /// Returns the current master count
        /// </summary>
        /// <returns></returns>
        public BigInteger GetMasterCount()
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            return masters.Size;
        }

        /// <summary>
        /// Returns the Masters addresses
        /// </summary>
        /// <returns></returns>
        public Address[] GetMasterAddresses()
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            return masters.GetMembers();
        }
        
        /// <summary>
        /// verifies how many valid masters are in the condition to claim the reward for a specific master claim date, assuming no changes in their master status in the meantime
        /// </summary>
        /// <param name="claimDate"></param>
        /// <returns></returns>
        public BigInteger GetClaimMasterCount(Timestamp claimDate)
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);

            var date = (DateTime)claimDate;
            DateTime requestedClaimDate = new DateTime(date.Year, date.Month, 1);

            var addresses = masters.GetMembers();
            var count = addresses.Length;
            var result = count;

            for (int i = 0; i < count; i++)
            {
                var addr = addresses[i];
                var currentMasterClaimDate = (DateTime)_masterClaims.Get<Address, Timestamp>(addr);
                if (currentMasterClaimDate > requestedClaimDate)
                {
                    result--;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the current master for a given claim distance
        /// </summary>
        /// <param name="claimDistance"></param>
        /// <returns></returns>
        public Timestamp GetMasterClaimDate(BigInteger claimDistance)
        {
            return GetMasterClaimDateFromReference(claimDistance, default);
        }

        /// <summary>
        /// Returns the master claim date for a given address
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public Timestamp GetMasterClaimDateForAddress(Address target)
        {
            if (_masterClaims.ContainsKey(target))
            {
                return _masterClaims.Get<Address, Timestamp>(target);
            }
            return new Timestamp(0);
        }

        /// <summary>
        /// Returns the master claim date for a given address
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public Timestamp GetMasterDate(Address target)
        {
            if (_masterAgeMap.ContainsKey(target))
            {
                return _masterAgeMap.Get<Address, Timestamp>(target);
            }

            return new Timestamp(0);
        }

        /// <summary>
        /// Returns master claim date from a reference date
        /// </summary>
        /// <param name="claimDistance"></param>
        /// <param name="referenceTime"></param>
        /// <returns></returns>
        public Timestamp GetMasterClaimDateFromReference(BigInteger claimDistance, Timestamp referenceTime)
        {
            DateTime referenceDate;
            if (referenceTime.Value != 0)
            {
                referenceDate = referenceTime;
            }
            else if (_lastMasterClaim.Value == 0)
            {
                if (Runtime.HasGenesis)
                {
                    Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
                    var referenceBlock = Runtime.GetBlockByHeight(1);
                    referenceDate = referenceBlock.Timestamp;
                }
                else
                {
                    referenceDate = Runtime.Time;
                }
                referenceDate = referenceDate.AddMonths(-1);
            }
            else
            {
                referenceDate = _lastMasterClaim;
            }

            var nextMasterClaim = (Timestamp)new DateTime(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths((int)claimDistance);
            var dateTimeClaim = (DateTime)nextMasterClaim;

            if (dateTimeClaim.Hour == 23)
                nextMasterClaim = dateTimeClaim.AddHours(1);
            if (dateTimeClaim.Hour == 1)
                nextMasterClaim = dateTimeClaim.AddHours(-1);

            //Allow a claim once per month starting on the 1st day of each month
            return nextMasterClaim;
        }

        /// <summary>
        /// Returns the masters rewards for the given address
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public BigInteger GetMasterRewards(Address from)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), "invalid master");

            var thisClaimDate = _masterClaims.Get<Address, Timestamp>(from);
            var totalAmount = MasterClaimGlobalAmount;
            var validMasterCount = GetClaimMasterCount(thisClaimDate);
            Runtime.Expect(validMasterCount > 0, "Validator count should be higher than 0");
            BigInteger individualAmount = 0;
            if (validMasterCount != 0)
                individualAmount = totalAmount / validMasterCount;
            else
                individualAmount = totalAmount / 1;
            var leftovers = totalAmount % validMasterCount;
            individualAmount += leftovers;

            return individualAmount;
        }

        /// <summary>
        /// migrates the full stake from one address to other
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void Migrate(Address from, Address to)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == DesiredPreviousContext, "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(!Nexus.IsDangerousAddress(from), "this address can't be used as source");
            Runtime.Expect(to.IsUser, "destination must be user address");

            var targetStake = GetStake(to);
            Runtime.Expect(targetStake == 0, "Tried to migrate to an account that's already staking");

            var unclaimed = GetUnclaimed(from);
            Runtime.Expect(unclaimed == 0, "claim before migrating");

            _stakeMap.Migrate<Address, EnergyStake>(from, to);
            _masterClaims.Migrate<Address, Timestamp>(from, to);
            _masterAgeMap.Migrate<Address, Timestamp>(from, to);
            _voteHistory.Migrate<Address, StorageList>(from, to);
            _leftoverMap.Migrate<Address, Timestamp>(from, to);
            _claimMap.Migrate<Address, StorageList>(from, to);

            Runtime.Notify(EventKind.AddressMigration, to, from);
        }

        /// <summary>
        /// Performs a master claim
        /// </summary>
        /// <param name="from"></param>
        public void MasterClaim(Address from)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), $"{from} is no SoulMaster");

            var thisClaimDate = _masterClaims.Get<Address, Timestamp>(from);
            Runtime.Expect(Runtime.Time >= thisClaimDate, "not enough time waited");

            var symbol = DomainSettings.StakingTokenSymbol;
            var token = Runtime.GetToken(symbol);

            var totalAmount = MasterClaimGlobalAmount;
            Runtime.MintTokens(token.Symbol, Address, Address, totalAmount);

            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            var validMasterCount = GetClaimMasterCount(Runtime.Time);

            if (Runtime.ProtocolVersion >= 10)
            {
                validMasterCount = GetClaimMasterCount(thisClaimDate);
            }

            var individualAmount = totalAmount / validMasterCount;
            var leftovers = totalAmount % validMasterCount;

            var nextClaim = GetMasterClaimDateFromReference(1, thisClaimDate);

            var addresses = masters.GetMembers();
            for (int i = 0; i < addresses.Length; i++)
            {
                var addr = addresses[i];
                var claimDate = _masterClaims.Get<Address, Timestamp>(addr);

                if (claimDate > thisClaimDate)
                {
                    continue;
                }

                var transferAmount = individualAmount;
                if (addr == from)
                {
                    transferAmount += leftovers;
                }

                Runtime.TransferTokens(token.Symbol, Address, addr, transferAmount);
                totalAmount -= transferAmount;

                _masterClaims.Set(addr, nextClaim);
            }

            Runtime.Expect(totalAmount == 0, $"Error on calculations, totalAmount should have been zero but it was {totalAmount} instead");

            _lastMasterClaim = Runtime.Time;

            Runtime.Notify(EventKind.MasterClaim, from, new MasterEventData(symbol, MasterClaimGlobalAmount, Runtime.Chain.Name, _lastMasterClaim));
        }

        /// <summary>
        /// Method used to stake tokens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="stakeAmount"></param>
        public void Stake(Address from, BigInteger stakeAmount)
        {
            Runtime.Expect(stakeAmount >= MinimumValidStake, "invalid amount");

            if (Runtime.ProtocolVersion <= 8)
            {
                var crownAddress = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
                Runtime.Expect(!Nexus.IsDangerousAddress(from, crownAddress), "this address can't be used as source");
            }
            else
            {
                if (Runtime.PreviousContext.Name == NativeContractKind.Gas.GetContractName())
                {
                    var validAddresses = new List<Address>();
                    var swapAddress = GetAddressForNative(NativeContractKind.Swap);
                    var exchangeAddress = GetAddressForNative(NativeContractKind.Exchange);
                    var crownAddress = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
                    var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
                    var bpOrg = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);

                    validAddresses.Add(crownAddress);
                    validAddresses.Add(swapAddress);
                    validAddresses.Add(exchangeAddress);
                    if (phantomOrg != null) validAddresses.Add(phantomOrg.Address);
                    if (bpOrg != null) validAddresses.Add(bpOrg.Address);

                    Runtime.Expect(!Nexus.IsDangerousAddress(from, validAddresses.ToArray()), "this address can't be used as source");
                }
                else
                {
                    var crownAddress = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
                    Runtime.Expect(!Nexus.IsDangerousAddress(from, crownAddress), "this address can't be used as source");
                    if (Runtime.ProtocolVersion >= 12)
                    {
                        if (from != crownAddress)
                        {
                            if (Runtime.HasGenesis)
                            {
                                Runtime.Expect(Runtime.IsWitness(from), "witness failed");
                            }
                            else
                            {
                                Runtime.Expect(Runtime.IsPrimaryValidator(from), "only primary validators can stake during genesis");
                            }
                        }
                        else
                        {
                            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
                        }
                    }
                }
            }

            if (Runtime.ProtocolVersion <= 11)
            {
                if (Runtime.HasGenesis)
                {
                    Runtime.Expect(Runtime.IsWitness(from), "witness failed");
                }
                else
                {
                    Runtime.Expect(Runtime.IsPrimaryValidator(from), "only primary validators can stake during genesis");
                }
            }

            var balance = Runtime.GetBalance(DomainSettings.StakingTokenSymbol, from);

            Runtime.Expect(balance >= stakeAmount, $"balance: {balance} stake: {stakeAmount} not enough balance to stake at {from}");

            Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, from, Address, stakeAmount);

            EnergyStake stake;

            if (_stakeMap.ContainsKey(from))
            {
                stake = _stakeMap.Get<Address, EnergyStake>(from);
            }
            else
            {
                stake = new EnergyStake()
                {
                    stakeTime = new Timestamp(0),
                    stakeAmount = 0,
                };
            }

            stake.stakeTime = Runtime.Time;
            stake.stakeAmount += stakeAmount;
            _stakeMap.Set(from, stake);

            Runtime.AddMember(DomainSettings.StakersOrganizationName, Address, from);

            var claimList = _claimMap.Get<Address, StorageList>(from);
            var claimEntry = new EnergyClaim()
            {
                stakeAmount = stakeAmount,
                claimDate = Runtime.Time,
                isNew = true,
            };
            claimList.Add(claimEntry);

            var logEntry = new VotingLogEntry()
            {
                timestamp = Runtime.Time,
                amount = stakeAmount
            };
            var votingLogbook = _voteHistory.Get<Address, StorageList>(from);
            votingLogbook.Add(logEntry);

            // masters membership
            var masterAccountThreshold = GetMasterThreshold();
            if (stake.stakeAmount >= masterAccountThreshold && !IsMaster(from))
            {
                var nextClaim = GetMasterClaimDate(2);

                Runtime.AddMember(DomainSettings.MastersOrganizationName, Address, from);
                _masterClaims.Set(from, nextClaim);

                _masterAgeMap.Set(from, Runtime.Time);
            }

            Runtime.Notify(EventKind.TokenStake, from, new TokenEventData(DomainSettings.StakingTokenSymbol, stakeAmount, Runtime.Chain.Name));
        }

        /// <summary>
        /// Method used to unstake tokens
        /// </summary>
        /// <param name="from"></param>
        /// <param name="unstakeAmount"></param>
        public void Unstake(Address from, BigInteger unstakeAmount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(unstakeAmount >= MinimumValidStake, "invalid amount");

            Runtime.Expect(!Nexus.IsDangerousAddress(from), "this address can't be used as source");

            Runtime.Expect(_stakeMap.ContainsKey(from), "nothing to unstake");

            var stake = _stakeMap.Get<Address, EnergyStake>(from);
            Runtime.Expect(stake.stakeAmount > 0, "nothing to unstake");

            Runtime.Expect(stake.stakeTime.Value > 0, "something weird happened in unstake"); // failsafe, should never happen

            Runtime.Expect(Runtime.Time >= stake.stakeTime, "Negative time diff");

            var stakedDiff = Runtime.Time - stake.stakeTime;
            var stakedDays = stakedDiff / SecondsInDay; // convert seconds to days

            Runtime.Expect(stakedDays >= 1, "waiting period required");

            var token = Runtime.GetToken(DomainSettings.StakingTokenSymbol);
            var balance = Runtime.GetBalance(token.Symbol, Address);
            Runtime.Expect(balance >= unstakeAmount, "not enough balance to unstake");

            var availableStake = stake.stakeAmount;
            availableStake -= GetStorageStake(from);
            Runtime.Expect(availableStake >= unstakeAmount, "tried to unstake more than what was staked");

            //if this is a partial unstake
            if (availableStake - unstakeAmount > 0)
            {
                Runtime.Expect(availableStake - unstakeAmount >= MinimumValidStake, "leftover stake would be below minimum staking amount");
            }

            Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, Address, from, unstakeAmount);

            stake.stakeAmount -= unstakeAmount;

            if (stake.stakeAmount == 0)
            {
                _stakeMap.Remove(from);
                _voteHistory.Remove(from);

                Runtime.RemoveMember(DomainSettings.StakersOrganizationName, Address, from);

                var name = Runtime.GetAddressName(from);
                if (name != ValidationUtils.ANONYMOUS_NAME)
                {
                    Runtime.CallNativeContext(NativeContractKind.Account, "UnregisterName", from);
                }
            }
            else
            {
                _stakeMap.Set(from, stake);

                RemoveVotingPower(from, unstakeAmount);
            }

            var masterAccountThreshold = GetMasterThreshold();

            if (stake.stakeAmount < masterAccountThreshold)
            {
                Runtime.RemoveMember(DomainSettings.MastersOrganizationName, Address, from);

                if (_masterClaims.ContainsKey(from))
                {
                    _masterClaims.Remove(from);
                }

                if (_masterAgeMap.ContainsKey(from))
                {
                    _masterAgeMap.Remove(from);
                }
            }

            var claimList = _claimMap.Get<Address, StorageList>(from);
            var count = claimList.Count();

            BigInteger leftovers = 0;

            while (unstakeAmount > 0)
            {
                int bestIndex = -1;
                var bestTime = new Timestamp(0);

                // find the oldest stake
                for (int i = 0; i < count; i++)
                {
                    var temp = claimList.Get<EnergyClaim>(i);
                    if (bestIndex == -1 || temp.claimDate < bestTime)
                    {
                        bestTime = temp.claimDate;
                        bestIndex = i;
                    }
                }

                Runtime.Expect(bestIndex >= 0, "something went wrong with unstake");

                var entry = claimList.Get<EnergyClaim>(bestIndex);

                BigInteger subtractedAmount;

                if (entry.stakeAmount > unstakeAmount)
                {
                    subtractedAmount = unstakeAmount;
                    entry.stakeAmount -= subtractedAmount;
                    claimList.Replace(bestIndex, entry);
                }
                else
                {
                    subtractedAmount = entry.stakeAmount;
                    claimList.RemoveAt(bestIndex);
                    count--;
                }


                if (Runtime.Time >= entry.claimDate)
                {
                    var claimDiff = Runtime.Time - entry.claimDate;
                    var claimDays = claimDiff / SecondsInDay;
                    if (!entry.isNew && claimDays > 0)
                    {
                        claimDays--;  // unless new (meaning was never claimed) we subtract the initial day due to instant claim
                    }

                    if (claimDays >= 1)
                    {
                        var amount = StakeToFuel(subtractedAmount);
                        amount *= claimDays;
                        leftovers += amount;
                    }
                }

                unstakeAmount -= subtractedAmount;
            }

            if (leftovers > 0)
            {
                if (_leftoverMap.ContainsKey(from))
                {
                    leftovers += _leftoverMap.Get<Address, BigInteger>(from);
                }

                _leftoverMap.Set(from, leftovers);
            }
        }

        /// <summary>
        /// Returns the amount of time left before unstaking is possible
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public BigInteger GetTimeBeforeUnstake(Address from)
        {
            if (!_stakeMap.ContainsKey(from))
            {
                return 0;
            }

            var stake = _stakeMap.Get<Address, EnergyStake>(from);
            return (Runtime.Time - stake.stakeTime) % SecondsInDay;
        }

        /// <summary>
        /// Returns the timestamp of when the stake was made
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public Timestamp GetStakeTimestamp(Address from)
        {
            if (!_stakeMap.ContainsKey(from))
            {
                return 0;
            }

            var stake = _stakeMap.Get<Address, EnergyStake>(from);
            return stake.stakeTime;
        }

        /// <summary>
        /// Removes voting power from the user
        /// </summary>
        /// <param name="from"></param>
        /// <param name="amount"></param>
        private void RemoveVotingPower(Address from, BigInteger amount)
        {
            var votingLogbook = _voteHistory.Get<Address, StorageList>(from);

            var listSize = votingLogbook.Count();

            for (var i = listSize - 1; i >= 0 && amount > 0; i--)
            {
                var votingEntry = votingLogbook.Get<VotingLogEntry>(i);

                if (votingEntry.amount > amount)
                {
                    votingEntry.amount -= amount;
                    votingLogbook.Replace(i, votingEntry);

                    amount = 0;
                }
                else
                {
                    amount -= votingEntry.amount;
                    votingLogbook.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Returns the unclaimed amount.
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public BigInteger GetUnclaimed(Address from)
        {
            BigInteger total = 0;

            var claimList = _claimMap.Get<Address, StorageList>(from);

            uint[] crownDays;

            var crowns = Runtime.GetOwnerships(DomainSettings.RewardTokenSymbol, from);

            // calculate how many days each CROWN is hold at current address and use older ones first
            if (Runtime.ProtocolVersion <= 12)
            {
                crownDays = crowns.Select(id => (Runtime.Time - Runtime.ReadToken(DomainSettings.RewardTokenSymbol, id).Timestamp) / SecondsInDay).OrderByDescending(k => k).ToArray();
            }
            else
            {
                crownDays = crowns.Select(id => id != 0 ?

                        (Runtime.Time - Runtime.ReadToken(DomainSettings.RewardTokenSymbol, id).Timestamp) / SecondsInDay :
                        0
                    ).OrderBy(k => k).ToArray();
            }

            var bonusPercent = (int)Runtime.GetGovernanceValue(StakeSingleBonusPercentTag);
            var maxPercent = (int)Runtime.GetGovernanceValue(StakeMaxBonusPercentTag);

            var count = claimList.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = claimList.Get<EnergyClaim>(i);

                if (Runtime.Time >= entry.claimDate)
                {
                    var claimDiff = Runtime.Time - entry.claimDate;
                    var claimDays = claimDiff / SecondsInDay;
                    if (entry.isNew)
                    {
                        claimDays++;
                    }

                    if (claimDays >= 1)
                    {
                        var amount = StakeToFuel(entry.stakeAmount);
                        amount *= claimDays;
                        total += amount;

                        int bonusAccum = 0;
                        var bonusAmount = amount * bonusPercent / 100;

                        var dailyBonus = bonusAmount / claimDays;

                        foreach (var bonusDays in crownDays)
                        {
                            if (bonusDays >= 1)
                            {
                                bonusAccum += bonusPercent;
                                if (bonusAccum > maxPercent)
                                {
                                    break;
                                }

                                var maxBonusDays = bonusDays > claimDays ? claimDays : bonusDays;
                                total += dailyBonus * maxBonusDays;
                            }
                        }
                    }
                }
            }


            if (_leftoverMap.ContainsKey(from))
            {
                var leftover = _leftoverMap.Get<Address, BigInteger>(from);
                total += leftover;
            }

            return total;
        }

        /// <summary>
        /// Method used to claim the unclaimed amount.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="stakeAddress"></param>
        public void Claim(Address from, Address stakeAddress)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            var unclaimedAmount = GetUnclaimed(stakeAddress);

            Runtime.Expect(unclaimedAmount > 0, "nothing unclaimed");

            var fuelAmount = unclaimedAmount;

            // if the transaction comes from someone other than the stake owner, must be a contract / organizataion
            if (from != stakeAddress)
            {
                Runtime.Expect(stakeAddress.IsSystem, "must claim from a system address");
            }

            Runtime.MintTokens(DomainSettings.FuelTokenSymbol, Address, stakeAddress, fuelAmount);

            var claimList = _claimMap.Get<Address, StorageList>(stakeAddress);
            var count = claimList.Count();

            // update the date of everything that was claimed
            for (int i = 0; i < count; i++)
            {
                var entry = claimList.Get<EnergyClaim>(i);

                if (Runtime.Time >= entry.claimDate)
                {
                    var claimDiff = Runtime.Time - entry.claimDate;
                    var clamDays = claimDiff / SecondsInDay;
                    if (entry.isNew)
                    {
                        clamDays++;
                    }

                    if (clamDays >= 1)
                    {
                        entry.claimDate = Runtime.Time;
                        entry.isNew = false;
                        claimList.Replace(i, entry);
                    }
                }
            }

            // remove any leftovers
            if (_leftoverMap.ContainsKey(stakeAddress))
            {
                _leftoverMap.Remove(stakeAddress);
            }

            // mark date to prevent imediate unstake
            if (Runtime.Time >= ContractPatch.UnstakePatch)
            {
                Runtime.Expect(_stakeMap.ContainsKey(stakeAddress), "invalid stake address");
                var stake = _stakeMap.Get<Address, EnergyStake>(stakeAddress);
                stake.stakeTime = Runtime.Time;
                _stakeMap.Set(stakeAddress, stake);
            }
        }

        /// <summary>
        /// Returns the stake amount for a given address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public BigInteger GetStake(Address address)
        {
            BigInteger stake = 0;

            if (_stakeMap.ContainsKey(address))
            {
                stake = _stakeMap.Get<Address, EnergyStake>(address).stakeAmount;
            }

            return stake;
        }

        /// <summary>
        /// Returns the Storage stake amount for a given address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public BigInteger GetStorageStake(Address address)
        {
            var usedStorageSize = Runtime.CallNativeContext(NativeContractKind.Storage, "GetUsedSpace", address).AsNumber();
            var usedStake = usedStorageSize * UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);

            var kilobytesPerStake = (int)Runtime.GetGovernanceValue(StorageContract.KilobytesPerStakeTag);
            usedStake = usedStake / (kilobytesPerStake * 1024);

            return usedStake;
        }

        /// <summary>
        /// Returns the Staked amount from a fuel amount.
        /// </summary>
        /// <param name="fuelAmount"></param>
        /// <returns></returns>
        public BigInteger FuelToStake(BigInteger fuelAmount)
        {
            return UnitConversion.ConvertDecimals(fuelAmount * _currentEnergyRatioDivisor, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals);
        }

        /// <summary>
        /// Returns the Fuel amount from a staked amount.
        /// </summary>
        /// <param name="stakeAmount"></param>
        /// <returns></returns>
        public BigInteger StakeToFuel(BigInteger stakeAmount)
        {
            return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _currentEnergyRatioDivisor;
        }

        /// <summary>
        /// Returns the Staked amount from a fuel amount.
        /// </summary>
        /// <param name="fuelAmount"></param>
        /// <param name="_BaseEnergyRatioDivisor"></param>
        /// <returns></returns>
        public static BigInteger FuelToStake(BigInteger fuelAmount, uint _BaseEnergyRatioDivisor)
        {
            return UnitConversion.ConvertDecimals(fuelAmount * _BaseEnergyRatioDivisor, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals);
        }

        /// <summary>
        /// Returns the Fuel amount from a staked amount.
        /// </summary>
        /// <param name="stakeAmount"></param>
        /// <param name="_BaseEnergyRatioDivisor"></param>
        /// <returns></returns>
        public static BigInteger StakeToFuel(BigInteger stakeAmount, uint _BaseEnergyRatioDivisor)
        {
            return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _BaseEnergyRatioDivisor;
        }

        /// <summary>
        /// Returns the voting power for a given address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public BigInteger GetAddressVotingPower(Address address)
        {
            var requiredVotingThreshold = Runtime.GetGovernanceValue(VotingStakeThresholdTag);
            if (GetStake(address) < requiredVotingThreshold)
            {
                return 0;
            }

            var votingLogbook = _voteHistory.Get<Address, StorageList>(address);
            BigInteger power = 0;

            var listSize = votingLogbook.Count();
            var time = Runtime.Time;

            for (int i = 0; i < listSize; i++)
            {
                var entry = votingLogbook.Get<VotingLogEntry>(i);

                if (i > 0)
                    Runtime.Expect(votingLogbook.Get<VotingLogEntry>(i - 1).timestamp <= entry.timestamp, "Voting list became unsorted!");

                power += CalculateEntryVotingPower(entry, time);
            }

            return power;
        }

        /// <summary>
        /// Returns the voting power for a given address.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        private BigInteger CalculateEntryVotingPower(VotingLogEntry entry, Timestamp currentTime)
        {
            BigInteger baseMultiplier = 100;

            BigInteger votingMultiplier = baseMultiplier;
            var diff = (currentTime - entry.timestamp) / 86400;

            var votingBonus = diff < MaxVotingPowerBonus ? diff : MaxVotingPowerBonus;

            votingMultiplier += DailyVotingBonus * votingBonus;

            var votingPower = entry.amount * votingMultiplier / 100;

            return votingPower;
        }

        /*public void UpdateRate(BigInteger rate)
        {
            var bombAddress = GetAddressFromContractName("bomb");
            Runtime.Expect(Runtime.IsWitness(bombAddress), "must be called from bomb address");

            Runtime.Expect(rate > 0, "invalid rate");
            _currentEnergyRatioDivisor = rate;
        }*/

        /// <summary>
        /// Returns the current rate.
        /// </summary>
        /// <returns></returns>
        public BigInteger GetRate()
        {
            return _currentEnergyRatioDivisor;
        }

        /// <summary>
        /// Returns the last master claim timestamp.
        /// </summary>
        /// <returns></returns>
        public Timestamp GetLastMasterClaim()
        {
            return _lastMasterClaim;
        }
    }
}
