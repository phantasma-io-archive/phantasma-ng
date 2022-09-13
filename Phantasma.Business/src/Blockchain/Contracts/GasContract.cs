using System.Collections.Generic;
using System.Numerics;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.VM;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Shared.Performance;
using Phantasma.Shared.Types;

namespace Phantasma.Business.Blockchain.Contracts
{
    public struct GasLoanEntry
    {
        public Hash hash;
        public Address borrower;
        public Address lender;
        public BigInteger amount;
        public BigInteger interest;
    }

    public struct GasLender
    {
        public BigInteger balance;
        public Address paymentAddress;
    }

    public sealed class GasContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Gas;

#pragma warning disable 0649
        internal StorageMap _allowanceMap; //<Address, BigInteger>
        internal StorageMap _allowanceTargets; //<Address, Address>
#pragma warning restore 0649

        internal BigInteger _rewardAccum;

        internal Timestamp _lastInflationDate;
        internal bool _inflationReady;

        /// <summary>
        /// Method used the usage of Gas to do the transaction.
        /// </summary>
        /// <param name="from">Address of the user</param>
        /// <param name="target">Address of the target</param>
        /// <param name="price">Gas Price of the transaction</param>
        /// <param name="limit">Gas Limit of the transaction</param>
        /// <exception cref="BalanceException"></exception>
        public void AllowGas(Address from, Address target, BigInteger price, BigInteger limit)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            if (_lastInflationDate == 0)
            {
                _lastInflationDate = Runtime.Time;
            }

            Runtime.Expect(from.IsUser, "must be a user address");
            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName, "must be entry context");
            Runtime.Expect(Runtime.IsWitness(from), $"invalid witness -> {from}");
            Runtime.Expect(target.IsSystem, "destination must be system address");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            if (target.IsNull)
            {
                target = Runtime.Chain.Address;
            }

            var maxAmount = price * limit;

            using (var m = new ProfileMarker("_allowanceMap"))
            {
                var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
                Runtime.Expect(allowance == 0, "unexpected pending allowance");

                allowance += maxAmount;
                _allowanceMap.Set(from, allowance);
                _allowanceTargets.Set(from, target);
            }

            BigInteger balance;
            using (var m = new ProfileMarker("Runtime.GetBalance"))
            {
                balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);
            }

            if (maxAmount > balance)
            {
                var diff = maxAmount - balance;
                var fuelToken = Runtime.GetToken(DomainSettings.FuelTokenSymbol);
                throw new BalanceException(fuelToken, from, diff);
            }

            Runtime.Expect(balance >= maxAmount, $"not enough {DomainSettings.FuelTokenSymbol} {balance} in address {from} {maxAmount}");

            using (var m = new ProfileMarker("Runtime.TransferTokens"))
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, maxAmount);
            using (var m = new ProfileMarker("Runtime.Notify"))
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
            var inflationAmount = currentSupply / 133;
            BigInteger mintedAmount = 0;

            Runtime.Expect(inflationAmount > 0, "invalid inflation amount");
            
            var masterOrg = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            var masters = masterOrg.GetMembers();
            
            var rewardList = new List<Address>();
            foreach (var addr in masters)
            {
                var masterDate = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetMasterDate), addr).AsTimestamp();

                if (masterDate <= _lastInflationDate)
                {
                    rewardList.Add(addr);
                }
            }

            if (rewardList.Count > 0)
            {
                var rewardAmount = inflationAmount / 10;

                var rewardStake = rewardAmount / rewardList.Count;
                rewardAmount = rewardList.Count * rewardStake; // eliminate leftovers

                var rewardFuel = _rewardAccum / rewardList.Count;

                BigInteger stakeAmount;

                stakeAmount = UnitConversion.ToBigInteger(2, DomainSettings.StakingTokenDecimals);

                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, this.Address, rewardAmount);

                var crownAddress = TokenUtils.GetContractAddress(DomainSettings.RewardTokenSymbol);
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, crownAddress, stakeAmount);
                Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), crownAddress, stakeAmount);

                foreach (var addr in rewardList)
                {
                    var reward = new StakeReward(addr, Runtime.Time);

                    /*var temp = VMObject.FromObject(reward);
                    temp = VMObject.CastTo(temp, VMType.Struct);

                    var rom = temp.Serialize();*/

                    var rom = Serialization.Serialize(reward);

                    var tokenID = Runtime.MintToken(DomainSettings.RewardTokenSymbol, this.Address, this.Address, rom, new byte[0], 0);
                    Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, this.Address, tokenID, DomainSettings.FuelTokenSymbol, rewardFuel);
                    Runtime.InfuseToken(DomainSettings.RewardTokenSymbol, this.Address, tokenID, DomainSettings.StakingTokenSymbol, rewardStake);
                    Runtime.TransferToken(DomainSettings.RewardTokenSymbol, this.Address, addr, tokenID);
                }

                _rewardAccum -= rewardList.Count * rewardFuel;
                Runtime.Expect(_rewardAccum >= 0, "invalid reward leftover");

                inflationAmount -= rewardAmount;
                inflationAmount -= stakeAmount;
            }

            var refillAmount = inflationAmount / 50;
            var cosmicAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);
            Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, cosmicAddress, refillAmount);
            inflationAmount -= refillAmount;

            var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
            if (phantomOrg != null)
            {
                var phantomFunding = inflationAmount / 3;
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, phantomOrg.Address, phantomFunding);
                inflationAmount -= phantomFunding;

                if (phantomOrg.Size == 1)
                {
                    Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), phantomOrg.Address, phantomFunding);
                }
            }

            var bpOrg = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            if (bpOrg != null)
            {
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, bpOrg.Address, inflationAmount);

                if (bpOrg.Size == 1)
                {
                    Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.Stake), bpOrg.Address, inflationAmount);
                }
            }

            Runtime.Notify(EventKind.Inflation, from, new TokenEventData(DomainSettings.StakingTokenSymbol, mintedAmount, Runtime.Chain.Name));

            _lastInflationDate = Runtime.Time;
            _inflationReady = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from">Address of the user</param>
        public void SpendGas(Address from)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName, "must be entry context");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(_allowanceMap.ContainsKey(from), "no gas allowance found");

            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;
            Runtime.Expect(requiredAmount > 0, $"{Runtime.GasPrice} {spentGas} gas fee must exist");

            Runtime.Expect(availableAmount >= requiredAmount, "gas allowance is not enough");

            var leftoverAmount = availableAmount - requiredAmount;

            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            BigInteger targetGas;

            Runtime.Notify(EventKind.GasPayment, from, new GasEventData(targetAddress,  Runtime.GasPrice, spentGas));

            // return leftover escrowed gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, leftoverAmount);
            }

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var burnGas = spentGas / 2;

            if (burnGas > 0)
            {
                BigInteger burnAmount;
                
                burnAmount = burnGas * Runtime.GasPrice;

                Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, this.Address, burnAmount);
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
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, targetAddress, targetPayment);
                }
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            {
                var validatorPayment = spentGas * Runtime.GasPrice;
                var validatorAddress = SmartContract.GetAddressForNative(NativeContractKind.Block);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, validatorAddress, validatorPayment);
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
            }
            else if (!_inflationReady)
            {
                var infDiff = Runtime.Time - _lastInflationDate;
                var inflationPeriod = SecondsInDay * 90;
                if (infDiff >= inflationPeriod)
                {
                    _inflationReady = true;
                }
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
        /// Method use to return how many days are left until the next distribution.
        /// </summary>
        /// <returns></returns>
        public uint GetDaysUntillDistribution()
        {
            return Runtime.Time - _lastInflationDate;
        }
    }
}
