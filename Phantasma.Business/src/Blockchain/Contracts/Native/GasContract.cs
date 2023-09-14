using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Gas;
using Phantasma.Core.Domain.Contract.Gas.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Numerics;
using Phantasma.Core.Performance;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;

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

        internal Address _ecosystemAddress;
        internal Address _leftoversAddress;
        
        private readonly int InflationMultiplier = 75;
        private readonly int InflationDivider = 10000;
        private readonly int InflationPerYear = 133;
        private readonly int V1_TokensToCosmicSwapPercentage = 50;
        private readonly int V1_SMInflationPercentage = 10;
        private readonly int V1_PhantasmaForcePercentage = 10;
        
        private readonly int V2_EcosystemPercentage = 33;
        private readonly int V2_SMInflationPercentage = 20;
        private readonly int V2_PhantasmaForcePercentage = 33;
        private readonly int V2_BPPercentage = 33;
        private readonly int V2_LeftOversPercent = 1;
        
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
            
            if (Runtime.ProtocolVersion < 16)
            {
                ApplyInflationV1(from, ref inflationAmount, ref mintedAmount);
            }
            else
            {
                // NOTE: this approximate inflation of 3% per year (0.75% per season)
                inflationAmount = currentSupply * InflationMultiplier / InflationDivider;
                ApplyInflationV2(from, ref inflationAmount, ref mintedAmount);
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
        /// Old Method used to Apply Inflation and Mint Crowns and distribute them.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="inflationAmount"></param>
        private void ApplyInflationV1(Address from, ref BigInteger inflationAmount, ref BigInteger mintedAmount)
        {
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
                var rewardAmount = inflationAmount / V1_SMInflationPercentage;

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

            var refillAmount = inflationAmount / V1_TokensToCosmicSwapPercentage;
            var cosmicAddress = GetAddressForNative(NativeContractKind.Swap);
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, cosmicAddress, refillAmount);
            inflationAmount -= refillAmount;

            var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
            if (phantomOrg != null)
            {
                var phantomFunding = inflationAmount / V1_PhantasmaForcePercentage;
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
        }

        /// <summary>
        /// New version of the Apply Inflation and Mint Crowns and distribute them.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="inflationAmount"></param>
        /// <param name="mintedAmount"></param>
        private void ApplyInflationV2(Address from, ref BigInteger inflationAmount, ref BigInteger mintedAmount)
        {
            Runtime.Expect(V2_EcosystemPercentage + V2_PhantasmaForcePercentage + V2_BPPercentage + V2_LeftOversPercent < 101, "invalid inflation percentages");
            
            var ecosystemInflationAmount = inflationAmount *  V2_EcosystemPercentage / 100;
            Runtime.Expect(ecosystemInflationAmount > 0, "invalid ecosystem inflation amount");

            var masterInflationAmount = ecosystemInflationAmount *  V2_SMInflationPercentage / 100;
            Runtime.Expect(masterInflationAmount > 0, "invalid master inflation amount");
            Runtime.Expect(masterInflationAmount < ecosystemInflationAmount, "invalid master inflation amount");

            var ecosystemLeftover = ecosystemInflationAmount - masterInflationAmount;
            Runtime.Expect(ecosystemLeftover > 0, "invalid ecosystem leftovers inflation amount");

            var phantomForceInflationAmount = inflationAmount * V2_PhantasmaForcePercentage / 100;
            Runtime.Expect(phantomForceInflationAmount > 0, "invalid phantom force inflation amount");

            var leftoversPercentAmount = inflationAmount  * V2_LeftOversPercent / 100;
            Runtime.Expect(leftoversPercentAmount > 0, "invalid leftovers amount");

            var bpInflationAmount = inflationAmount * V2_BPPercentage / 100;
            Runtime.Expect(bpInflationAmount > 0, "invalid bp inflation amount");

            var leftoverAmount = inflationAmount - ecosystemInflationAmount - phantomForceInflationAmount - leftoversPercentAmount - bpInflationAmount;
            
            Runtime.Expect(inflationAmount == 
                           (ecosystemInflationAmount + phantomForceInflationAmount + bpInflationAmount + leftoversPercentAmount + leftoverAmount), 
                "invalid inflation amount");

            HandleMastersOrganization(ref masterInflationAmount);
            
            HandleEcosystemLeftovers(ref ecosystemLeftover);
            
            var allLeftoversAmount = leftoverAmount + leftoversPercentAmount;
            HandleLeftoversAmounts(ref allLeftoversAmount);

            HandlePhantomForce(ref phantomForceInflationAmount);
            
            HandleBPOrganization(ref bpInflationAmount);

            mintedAmount = inflationAmount;
        }

        /// <summary>
        /// Ecosystem Leftovers
        /// </summary>
        /// <param name="ecosystemLeftover"></param>
        private void HandleEcosystemLeftovers(ref BigInteger ecosystemLeftover)
        {
            if ( _ecosystemAddress.Text == Address.NullText )
            {
                _ecosystemAddress = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName).Address;
            }
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, _ecosystemAddress, ecosystemLeftover);
        }

        /// <summary>
        /// Handle Soul Master Organization Rewards
        /// </summary>
        /// <param name="inflationAmount"></param>
        private void HandleMastersOrganization(ref BigInteger inflationAmount)
        {
            var masterOrg = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            var masters = masterOrg.GetMembers();

            var rewardList = new List<Address>();
            foreach (var addr in masters)
            {
                var masterDate = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetMasterDate), addr).AsTimestamp();
                // This is to check if the user is a master for more than 3 months (90 days)
                if (masterDate <= _lastInflationDate)
                {
                    rewardList.Add(addr);
                }
            }

            if (rewardList.Count == 0)
            {
                return;
            }

            // Mint Token and Stake for the Crown Contract. (Increase Storage)
            BigInteger stakeAmount = UnitConversion.ToBigInteger(2, DomainSettings.StakingTokenDecimals);
            var crownAddress = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, crownAddress, stakeAmount);
            Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), crownAddress, stakeAmount);
            
            // Calculate the Rewards
            var rewardAmount = inflationAmount - stakeAmount;

            var rewardStake = rewardAmount / rewardList.Count;
            rewardAmount = rewardList.Count * rewardStake; // eliminate leftovers

            var rewardFuel = _rewardAccum / rewardList.Count;

            _rewardAccum -= rewardList.Count * rewardFuel;
            Runtime.Expect(_rewardAccum >= 0, "invalid reward leftover");

            // Mint Tokens for the rewards
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, Address, rewardAmount);
            
            foreach (var addr in rewardList)
            {
                var reward = new StakeReward(addr, _nextInflationDate.Value);
                var rom = reward.Serialize();

                var tokenID = Runtime.MintToken(DomainSettings.RewardTokenSymbol, Address, Address, rom, new byte[0], 0);
                Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, Address, tokenID, DomainSettings.FuelTokenSymbol, rewardFuel);
                Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, Address, tokenID, DomainSettings.StakingTokenSymbol, rewardStake);
                Runtime.TransferToken(DomainSettings.RewardTokenSymbol, Address, addr, tokenID);
            }

            inflationAmount -= rewardAmount; // Rewards
            inflationAmount -= stakeAmount; // 2 SOUL for storage
        }
        
        /// <summary>
        /// Handle the leftovers Amounts
        /// </summary>
        /// <param name="leftoversAmount"></param>
        private void HandleLeftoversAmounts(ref BigInteger leftoversAmount)
        {
            if ( _leftoversAddress.Text == Address.NullText )
            {
                _leftoversAddress = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName).Address;
            }
            
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, _leftoversAddress, leftoversAmount);
        }
        
        /// <summary>
        /// Handle Phantom Force Organization Rewards
        /// </summary>
        /// <param name="inflationAmount"></param>
        private void HandlePhantomForce(ref BigInteger phantomFunding)
        {
            var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
            if (phantomOrg != null)
            {
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, phantomOrg.Address, phantomFunding);

                if (phantomOrg.Size == 1)
                {
                    Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), phantomOrg.Address, phantomFunding);
                }
            }
        }
        
        /// <summary>
        /// Handle the BP Organization Rewards
        /// </summary>
        /// <param name="inflationAmount"></param>
        private void HandleBPOrganization(ref BigInteger bpInflationAmount)
        {
            var bpOrg = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            if (bpOrg != null)
            {
                var bpOrgMembers = bpOrg.GetMembers();
                var bpSize = bpOrgMembers.Length;
                var bpReward = bpInflationAmount / bpSize;
                
                foreach (var member in bpOrgMembers)
                {
                    if (!member.IsNull)
                        Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, member, bpReward);
                }
                
                bpInflationAmount -= bpReward * bpSize; // eliminate leftovers
                
                // Transfer the leftovers to the BP Organization
                if ( bpInflationAmount > 0 )
                    Runtime.MintTokens(DomainSettings.StakingTokenSymbol, Address, bpOrg.Address, bpInflationAmount);
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
        /// Set the Ecosystem Address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="ecosystemAddress"></param>
        public void SetEcosystemAddress(Address from, Address ecosystemAddress)
        {
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(Runtime.IsKnownValidator(from), "invalid validator");
            //Runtime.Expect(ecosystemAddress.IsSystem, "invalid address");
            
            var org = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            var orgMembers = org.GetMembers();
            
            Runtime.Expect(org != null, "no validators org");
            Runtime.Expect(org.IsMember(from), "not a validator");
            Runtime.Expect(Runtime.Transaction.Signatures.Length == orgMembers.Length, "must be signed by all org members");
            Runtime.Expect(Runtime.Transaction.IsSignedByEveryone(orgMembers), "Invalid Signatures. Must be signed by all org members");
            
            _ecosystemAddress = ecosystemAddress;
        }

        /// <summary>
        /// Set Leftovers Address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="leftoversAddress"></param>
        public void SetLeftoversAddress(Address from, Address leftoversAddress)
        {
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(Runtime.IsKnownValidator(from), "invalid validator");
            //Runtime.Expect(leftoversAddress.IsSystem, "invalid address");
            
            var org = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            var orgMembers = org.GetMembers();
            
            Runtime.Expect(org != null, "no validators org");
            Runtime.Expect(org.IsMember(from), "not a validator");
            Runtime.Expect(Runtime.Transaction.Signatures.Length == orgMembers.Length, "must be signed by all org members");
            Runtime.Expect(Runtime.Transaction.IsSignedByEveryone(orgMembers), "Invalid Signatures. Must be signed by all org members");
            
            _leftoversAddress = leftoversAddress;
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
