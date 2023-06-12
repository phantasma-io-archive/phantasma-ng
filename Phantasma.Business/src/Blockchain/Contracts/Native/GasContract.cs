using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Gas;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Numerics;
using Phantasma.Core.Performance;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class GasContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Gas;

#pragma warning disable 0649
        internal StorageMap _allowanceMap; //<Address, BigInteger>
        internal StorageMap _allowanceTargets; //<Address, Address>
#pragma warning restore 0649

        internal BigInteger _rewardAccum;

        internal Timestamp _lastInflationDate;
        internal Timestamp _nextInflationDate;
        internal bool _inflationReady;
        internal bool _fixedInflation;

        private readonly int InflationPerYear = 133;
        private readonly int SMInflationPercentage = 10;
        private readonly int PhantasmaForcePercentage = 10;
        private readonly int TokensToCosmicSwapPercentage = 50;

        /// <summary>
        /// Method to check if an address has allowed gas
        /// </summary>
        /// <param name="from">Address of the user</param>
        public BigInteger AllowedGas(Address from)
        {
            var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
            return allowance;
        }

        /// <summary>
        /// Method used the usage of Gas to do the transaction.
        /// </summary>
        /// <exception cref="BalanceException"></exception>
        public void AllowGas(Address from, Address target, BigInteger price, BigInteger limit)
        {
            /*if (Runtime.IsReadOnlyMode())
            {
                return;
            }*/

            Runtime.Expect(from.IsUser, "must be a user address");
            Runtime.Expect(target.IsSystem, "destination must be system address");
            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            if (_lastInflationDate == 0)
            {
                _lastInflationDate = Runtime.GetGenesisTime();
                if (Runtime.ProtocolVersion >= 12)
                {
                    _nextInflationDate = new Timestamp(_lastInflationDate.Value + SecondsInDay * 90);
                }
            }

            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName, $"must be entry context {Runtime.PreviousContext.Name}");
            Runtime.Expect(Runtime.IsWitness(from), $"invalid witness -> {from}");

            if (target.IsNull)
            {
                target = Runtime.Chain.Address;
            }

            var maxAmount = price * limit;

            var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
            Runtime.Expect(allowance == 0, "unexpected pending allowance");

            allowance += maxAmount;
            _allowanceMap.Set(from, allowance);
            _allowanceTargets.Set(from, target);

            BigInteger balance;
            balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);

            Runtime.Expect(balance >= maxAmount, $"not enough {DomainSettings.FuelTokenSymbol} {balance} in address {from} {maxAmount}");

            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, Address, maxAmount);
            Runtime.Notify(EventKind.GasEscrow, from, new GasEventData(target, price, limit));
        }

        /// <summary>
        /// Method used to Apply Inflation and Mint Crowns and distribute them.
        /// </summary>
        /// <param name="from">Address of the user</param>
        public void ApplyInflation(Address from)
        {
            Runtime.Expect(_inflationReady, "inflation not ready");

            Runtime.Expect(Runtime.IsRootChain(), "only on root chain");

            var currentSupply = Runtime.GetTokenSupply(DomainSettings.StakingTokenSymbol);

            var minExpectedSupply = UnitConversion.ToBigInteger(100000000, DomainSettings.StakingTokenDecimals);
            if (currentSupply < minExpectedSupply)
            {
                currentSupply = minExpectedSupply;
            }

            // NOTE this gives an approximate inflation of 3% per year (0.75% per season)
            var inflationAmount = currentSupply / InflationPerYear;
            BigInteger mintedAmount = 0;

            Runtime.Expect(inflationAmount > 0, "invalid inflation amount");

            var masterOrg = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            var masters = masterOrg.GetMembers();

            var rewardList = new List<Address>();
            foreach (var addr in masters)
            {
                var masterDate = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetMasterDate), addr).AsTimestamp();
                // This is to check if the user is a master for more than 3 months (90 days)
                if (Runtime.ProtocolVersion >= 14)
                {
                    if (masterDate <= _lastInflationDate)
                    {
                        rewardList.Add(addr);
                    }
                }
                else if (Runtime.ProtocolVersion == 12)
                {
                    if (masterDate <= _nextInflationDate)
                    {
                        rewardList.Add(addr);
                    }
                }
                else
                {
                    if (masterDate <= _lastInflationDate)
                    {
                        rewardList.Add(addr);
                    }
                }
            }

            if (rewardList.Count > 0)
            {
                var rewardAmount = inflationAmount / SMInflationPercentage;

                var rewardStake = rewardAmount / rewardList.Count;
                rewardAmount = rewardList.Count * rewardStake; // eliminate leftovers

                var rewardFuel = _rewardAccum / rewardList.Count;

                _rewardAccum -= rewardList.Count * rewardFuel;
                Runtime.Expect(_rewardAccum >= 0, "invalid reward leftover");

                BigInteger stakeAmount;

                stakeAmount = UnitConversion.ToBigInteger(2, DomainSettings.StakingTokenDecimals);

                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, Address, rewardAmount);

                var crownAddress = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, crownAddress, stakeAmount);
                Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), crownAddress, stakeAmount);

                foreach (var addr in rewardList)
                {
                    var reward = new StakeReward(addr, Runtime.Time);
                    if (Runtime.ProtocolVersion >= 12)
                    {
                        reward = new StakeReward(addr, _nextInflationDate.Value);
                    }

                    var rom = reward.Serialize();

                    var tokenID = Runtime.MintToken(DomainSettings.RewardTokenSymbol, Address, Address, rom, new byte[0], 0);
                    Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, Address, tokenID, DomainSettings.FuelTokenSymbol, rewardFuel);
                    Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, Address, tokenID, DomainSettings.StakingTokenSymbol, rewardStake);
                    Runtime.TransferToken(DomainSettings.RewardTokenSymbol, Address, addr, tokenID);
                }

                inflationAmount -= rewardAmount;
                inflationAmount -= stakeAmount;
            }

            var refillAmount = inflationAmount / TokensToCosmicSwapPercentage;
            var cosmicAddress = GetAddressForNative(NativeContractKind.Swap);
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, cosmicAddress, refillAmount);
            inflationAmount -= refillAmount;

            var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
            if (phantomOrg != null)
            {
                var phantomFunding = inflationAmount / PhantasmaForcePercentage;
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, phantomOrg.Address, phantomFunding);
                inflationAmount -= phantomFunding;

                if (phantomOrg.Size == 1)
                {
                    Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), phantomOrg.Address, phantomFunding);
                }
            }

            var bpOrg = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            if (bpOrg != null)
            {
                if (Runtime.ProtocolVersion <= 8)
                {
                    Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, bpOrg.Address, inflationAmount);

                    if (bpOrg.Size == 1)
                    {
                        Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), bpOrg.Address, inflationAmount);
                    }
                }
                else
                {
                    // NOTE: in protocol 9, inflation is distributed to validators, not to the BP org
                    var bpOrgMembers = bpOrg.GetMembers();
                    var bpSize = bpOrgMembers.Length;
                    var bpReward = inflationAmount / bpSize;
                    foreach (var member in bpOrgMembers)
                    {
                        if (!member.IsNull)
                            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, member, bpReward);
                    }
                }
            }

            Runtime.Notify(EventKind.Inflation, from, new TokenEventData(DomainSettings.StakingTokenSymbol, mintedAmount, Runtime.Chain.Name));

            if (Runtime.ProtocolVersion >= 12)
            {
                var inflationPeriod = SecondsInDay * 90;
                _lastInflationDate = _nextInflationDate;
                _nextInflationDate = new Timestamp(_nextInflationDate.Value + inflationPeriod);
                if (Runtime.Time >= _nextInflationDate)
                {
                    _inflationReady = true;
                }
                else
                {
                    _inflationReady = false;
                }
            }
            else
            {
                _lastInflationDate = Runtime.Time;

                _inflationReady = false;
            }
        }

        /// <summary>
        /// Spend the Gas consumed by the scripts that were executed.
        /// </summary>
        public void SpendGas(Address from)
        {
            /*if (Runtime.IsReadOnlyMode())
            {
                return;
            }*/

            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName || Runtime.PreviousContext.Address.IsSystem,
                    $"must be entry context, prev: {Runtime.PreviousContext.Name}, curr: {Runtime.CurrentContext.Name}");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(_allowanceMap.ContainsKey(from), "no gas allowance found");

            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;

            GasEventData ged = new GasEventData(Address.Null, 0, 0);
            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            if (availableAmount < requiredAmount && Runtime.IsError)
            {
                requiredAmount = availableAmount;
                ged = new GasEventData(targetAddress, Runtime.GasPrice, spentGas);
            }

            Runtime.Expect(requiredAmount > 0, $"{Runtime.GasPrice} {Runtime.UsedGas} gas fee must exist");

            Runtime.Expect(availableAmount >= requiredAmount, $"gas allowance is not enough {availableAmount}/{requiredAmount}");

            var leftoverAmount = availableAmount - requiredAmount;

            BigInteger targetGas;

            if (ged.address == Address.Null)
            {
                ged = new GasEventData(targetAddress, Runtime.GasPrice, Runtime.UsedGas);
            }

            Runtime.Notify(EventKind.GasPayment, from, ged);

            // return leftover escrowed gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, Address, from, leftoverAmount);
            }

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var burnGas = spentGas / 2;

            if (burnGas > 0)
            {
                BigInteger burnAmount;

                burnAmount = burnGas * Runtime.GasPrice;

                Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, Address, burnAmount);
                spentGas -= burnGas;
            }

            targetGas = spentGas / 2; // 50% for dapps (or reward accum if dapp not specified)

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;

                if (targetAddress == Runtime.Chain.Address)
                {
                    _rewardAccum += targetPayment;
                    Runtime.Notify(EventKind.CrownRewards, from, new TokenEventData(DomainSettings.FuelTokenSymbol, targetPayment, Runtime.Chain.Name));
                }
                else
                {
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, Address, targetAddress, targetPayment);
                }
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            { 
                EmitValidatorPayment(spentGas);
                spentGas = 0;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);

            CheckInflation();
        }

        /// <summary>
        /// Method used to check if the inflation is ready
        /// </summary>
        private void CheckInflation()
        {
            if (!Runtime.HasGenesis)
            {
                return;
            }

            if (_lastInflationDate.Value == 0)
            {
                var genesisTime = Runtime.GetGenesisTime();
                _lastInflationDate = genesisTime;
                if (Runtime.ProtocolVersion >= 12)
                {
                    _nextInflationDate = new Timestamp(genesisTime.Value + SecondsInDay * 90);
                }
            }
            else if (!_inflationReady)
            {
                if (Runtime.ProtocolVersion <= 11)
                {
                    var infDiff = Runtime.Time - _lastInflationDate;
                    var inflationPeriod = SecondsInDay * 90;
                    if (infDiff >= inflationPeriod)
                    {
                        _inflationReady = true;
                    }
                }
                else if (Runtime.ProtocolVersion >= 12)
                {
                    if (Runtime.Time >= _nextInflationDate)
                    {
                        _inflationReady = true;
                    }
                }
            }
        }

        /// <summary>
        /// This method is used to fix the inflation timing.
        /// </summary>
        public void FixInflationTiming(Address from, Timestamp lastInflationDate)
        {
            Runtime.ExpectWarning(!_fixedInflation, "inflation timing already fixed", from);

            // Validate Validaotrs and orgs and Multi signature
            var org = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            Runtime.Expect(org != null, "no validators org");
            Runtime.Expect(org.IsMember(from), "not a validator");
            Runtime.Expect(Runtime.IsPrimaryValidator(from), "not a primary validator");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var size = org.Size;
            var numberOfSignaturesNeeded = org.Size;
            Runtime.Expect(Runtime.Transaction.Signatures.Length == numberOfSignaturesNeeded, "invalid number of signatures");

            var validSignatures = 0;
            Signature lastSignature = null;
            var signatures = Runtime.Transaction.Signatures.ToList();
            var members = org.GetMembers();
            var msg = Runtime.Transaction.ToByteArray(false);

            foreach (var member in members)
            {
                foreach (var signature in signatures)
                {
                    if (signature.Verify(msg, member))
                    {
                        validSignatures++;
                        lastSignature = signature;
                        break;
                    }
                }

                if (lastSignature != null)
                    signatures.Remove(lastSignature);
            }

            Runtime.Expect(validSignatures == numberOfSignaturesNeeded, "invalid signatures");

            // Fix Values
            _fixedInflation = true;
            _lastInflationDate = lastInflationDate - SecondsInDay * 90;
            _nextInflationDate = lastInflationDate;
            _inflationReady = true;
        }

        /// <summary>
        /// Method used to emit the validator payment 
        /// </summary>
        /// <param name="spentGas"></param>
        private void EmitValidatorPayment(BigInteger spentGas)
        {
            if ( spentGas == 0)
                return;
            
            var validatorPayment = spentGas * Runtime.GasPrice;
            if (Runtime.ProtocolVersion >= 14)
            {
                Address validatorAddress = Runtime.Chain.ValidatorAddress;
                var eventData = new TokenEventData(DomainSettings.FuelTokenSymbol, validatorPayment, Runtime.Chain.Name);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, Address, validatorAddress, validatorPayment);
                Runtime.Notify(EventKind.TokenClaim, validatorAddress, Serialization.Serialize(eventData), "block");
            }
            else
            {
                Address validatorAddress = GetAddressForNative(NativeContractKind.Block);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, Address, validatorAddress, validatorPayment);
            }
        }

        /// <summary>
        /// Method used to return the last inflation date.
        /// </summary>
        /// <returns></returns>
        public Timestamp GetLastInflationDate()
        {
            return _lastInflationDate;
        }

        /// <summary>
        /// Method used to return the last inflation date.
        /// </summary>
        /// <returns></returns>
        public Timestamp GetNextInflationDate()
        {
            return _nextInflationDate;
        }

        /// <summary>
        /// Method use to return how many days are left until the next distribution.
        /// </summary>
        /// <returns></returns>
        public uint GetDaysNextUntilDistribution()
        {
            return Runtime.Time - _nextInflationDate;
        }

        /// <summary>
        /// Method use to return how many days are left until the next distribution.
        /// </summary>
        /// <returns></returns>
        public uint GetDaysUntilDistribution()
        {
            return Runtime.Time - _lastInflationDate;
        }
    }
}
