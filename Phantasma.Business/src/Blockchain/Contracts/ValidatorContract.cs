using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;

namespace Phantasma.Business.Blockchain.Contracts
{
    public sealed class ValidatorContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Validator;

        public const string ValidatorSlotsTag = "validator.slots";
        public static readonly BigInteger ValidatorSlotsDefault = 5;
        
        public const string ValidatorRotationTimeTag = "validator.rotation.time";
        public static readonly BigInteger ValidatorRotationTimeDefault = 120;

        public const string ValidatorPollTag = "elections";
        


#pragma warning disable 0649
        private StorageList _validators; // <ValidatorInfo>
#pragma warning restore 0649

        private int _initialValidatorCount => (int)ValidatorSlotsDefault;

        public ValidatorContract() : base()
        {
        }

        public ValidatorEntry[] GetValidators()
        {
            return _validators.All<ValidatorEntry>();
        }

        public ValidatorEntry GetCurrentValidator(string tendermintAddress)
        {
            var validators = GetValidators();

            foreach (var validator in validators)
            {
                if (validator.address.TendermintAddress == tendermintAddress)
                {
                    return validator;
                }
            }

            return new ValidatorEntry()
            {
                address = Address.Null,
                type = ValidatorType.Invalid,
                election = new Timestamp(0)
            };
        }

        public ValidatorType GetValidatorType(Address address)
        {
            var validators = GetValidators();

            foreach (var validator in validators)
            {
                if (validator.address == address)
                {
                    return validator.type;
                }
            }

            return ValidatorType.Invalid;
        }

        public BigInteger GetIndexOfValidator(Address address)
        {
            if (!address.IsUser)
            {
                return -1;
            }

            var validators = GetValidators();

            int index = 0;
            foreach (var validator in validators)
            {
                if (validator.address == address)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public int GetMaxTotalValidators()
        {
            if (Runtime.HasGenesis)
            {
                return (int)Runtime.GetGovernanceValue(ValidatorSlotsTag);
            }

            return _initialValidatorCount;
        }

        public ValidatorEntry GetValidatorByIndex(BigInteger index)
        {
            Runtime.Expect(index >= 0, "invalid validator index");

            var totalValidators = GetMaxTotalValidators();
            Runtime.Expect(index < totalValidators, $"invalid validator index {index} {totalValidators}");

            if (index < _validators.Count())
            {
                return _validators.Get<ValidatorEntry>(index);
            }

            return new ValidatorEntry()
            {
                address = Address.Null,
                type = ValidatorType.Invalid,
                election = new Timestamp(0)
            };
        }

        public BigInteger GetValidatorCount(ValidatorType type)
        {
            if (type == ValidatorType.Invalid)
            {
                return 0;
            }

            var max = GetMaxPrimaryValidators();
            var count = 0;
            for (int i = 0; i < max; i++)
            {
                var validator = GetValidatorByIndex(i);
                if (validator.type == type)
                {
                    count++;
                }
            }
            return count;
        }

        public BigInteger GetMaxPrimaryValidators()
        {
            if (Runtime.HasGenesis)
            {
                var maxValidators = Runtime.GetGovernanceValue(ValidatorSlotsTag);

                var result = (maxValidators * 10) / 25;

                if (maxValidators > 0 && result < 1)
                {
                    result = 1;
                }

                return result;
            }

            return _initialValidatorCount;
        }

        public BigInteger GetMaxSecondaryValidators()
        {
            if (Runtime.HasGenesis)
            {
                var maxValidators = Runtime.GetGovernanceValue(ValidatorSlotsTag);
                return maxValidators - GetMaxPrimaryValidators();
            }

            return 0;
        }

        // NOTE - witness not required, as anyone should be able to call this, permission is granted based on consensus
        public void SetValidator(Address target, BigInteger index, ValidatorType type)
        {
            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(type == ValidatorType.Primary || type == ValidatorType.Secondary, "invalid validator type");

            var currentType = GetValidatorType(target);
            Runtime.Expect(currentType != type, $"Already a {type} validator");

            var primaryValidators = GetValidatorCount(ValidatorType.Primary);
            var secondaryValidators = GetValidatorCount(ValidatorType.Secondary);

            Runtime.Expect(index >= 0, "invalid index");

            var totalValidators = GetMaxTotalValidators();
            Runtime.Expect(index <= totalValidators, "invalid index " + totalValidators);

            var expectedType = index <= GetMaxPrimaryValidators() ? ValidatorType.Primary : ValidatorType.Secondary;
            Runtime.Expect(type == expectedType, "unexpected validator type");

            if (primaryValidators > _initialValidatorCount) // for initial validators stake is not verified because it doesn't exist yet.
            {
                var requiredStake = Runtime.CallNativeContext(NativeContractKind.Stake, nameof(StakeContract.GetMasterThreshold), target).AsNumber();
                var stakedAmount = Runtime.GetStake(target);

                Runtime.Expect(stakedAmount >= requiredStake, "not enough stake");
            }

            if (index > 0)
            {
                var previous = _validators.Get<ValidatorEntry>(index - 1);
                Runtime.Expect(previous.type != ValidatorType.Invalid, "previous validator has unexpected status");
            }

            // check if we're expanding the validator set
            if (primaryValidators > _initialValidatorCount)
            {
                var isValidatorProposed = _validators.Get<ValidatorEntry>(index).type == ValidatorType.Proposed;

                if (isValidatorProposed)
                {
                    var currentEntry = _validators.Get<ValidatorEntry>(index);
                    if (currentEntry.type != ValidatorType.Proposed)
                    {
                        Runtime.Expect(currentEntry.type == ValidatorType.Invalid, "invalid validator state");
                        isValidatorProposed = false;
                    }
                }

                var firstValidator = GetValidatorByIndex(0).address;
                if (isValidatorProposed)
                {
                    if (primaryValidators > _initialValidatorCount)
                    {
                        Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
                    }
                    else
                    {
                        Runtime.Expect(Runtime.IsWitness(firstValidator), "invalid witness");
                    }
                }
                else
                {
                    if (primaryValidators > _initialValidatorCount)
                    {
                        var pollName = ConsensusContract.SystemPoll + ValidatorPollTag;
                        var obtainedRank = Runtime.CallNativeContext(NativeContractKind.Consensus, "GetRank", pollName, target).AsNumber();
                        Runtime.Expect(obtainedRank >= 0, "no consensus for electing this address");
                        Runtime.Expect(obtainedRank == index, "this address was elected at a different index");
                    }
                    else
                    {
                        Runtime.Expect(Runtime.IsWitness(firstValidator), "invalid validator witness");
                    }

                    type = ValidatorType.Proposed;
                }
            }
            else
            if (primaryValidators > 0)
            {
                var passedWitnessCheck = false;

                var validators = GetValidators();
                foreach (var validator in validators)
                {
                    if (validator.type == ValidatorType.Primary && Runtime.IsWitness(validator.address))
                    {
                        passedWitnessCheck = true;
                        break;
                    }
                }

                Runtime.Expect(passedWitnessCheck, "invalid validator witness");
            }

            var currentSize = _validators.Count();
            Runtime.Expect(currentSize == index, "validator list has unexpected size");

            var entry = new ValidatorEntry()
            {
                address = target,
                election = Runtime.Time,
                type = type,
            };
            _validators.Add<ValidatorEntry>(entry);

            if (type == ValidatorType.Primary)
            {
                var newValidators = GetValidatorCount(ValidatorType.Primary);
                Runtime.Expect(newValidators > primaryValidators, "number of primary validators did not change");
            }
            else if (type == ValidatorType.Secondary)
            {
                var newValidators = GetValidatorCount(ValidatorType.Secondary);
                Runtime.Expect(newValidators > secondaryValidators, "number of secondary validators did not change");
            }

            if (type != ValidatorType.Proposed)
            {
                Runtime.AddMember(DomainSettings.ValidatorsOrganizationName, this.Address, target);
            }

            Runtime.Notify(type == ValidatorType.Proposed ? EventKind.ValidatorPropose : EventKind.ValidatorElect, Runtime.Chain.Address, target);
        }

        /*public void DemoteValidator(Address target)
        {
            Runtime.Expect(false, "not fully implemented");

            Runtime.Expect(target.IsUser, "must be user address");
            Runtime.Expect(IsKnownValidator(target), "not a validator");

            var count = _validatorList.Count();
            Runtime.Expect(count > 1, "cant remove last validator");

            var entry = _validatorMap.Get<Address, ValidatorEntry>(target);

            bool brokenRules = false;

            var diff = Timestamp.Now - Runtime.Nexus.GetValidatorLastActivity(target);
            var maxPeriod = 3600 * 2; // 2 hours
            if (diff > maxPeriod)
            {
                brokenRules = true;
            }

            var requiredStake = EnergyContract.MasterAccountThreshold;
            var stakedAmount = (BigInteger)Runtime.CallContext(Nexus.StakeContractName, "GetStake", target);

            if (stakedAmount < requiredStake)
            {
                brokenRules = true;
            }

            Runtime.Expect(brokenRules, "no rules broken");

            _validatorMap.Remove(target);
            _validatorList.Remove(target);

            Runtime.Notify(EventKind.ValidatorRemove, Runtime.Chain.Address, target);
            Runtime.RemoveMember(DomainSettings.ValidatorsOrganizationName, this.Address, from, to);
        }*/

        public void Migrate(Address from, Address to)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == "account", "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            Runtime.Expect(to.IsUser, "destination must be user address");

            var index = GetIndexOfValidator(from);
            Runtime.Expect(index >= 0, "validator index not found");

            var entry = _validators.Get<ValidatorEntry>(index);
            Runtime.Expect(entry.type == ValidatorType.Primary || entry.type == ValidatorType.Secondary, "not active validator");

            entry.address = to;
            _validators.Replace<ValidatorEntry>(index, entry);

            Runtime.MigrateMember(DomainSettings.ValidatorsOrganizationName, this.Address, from, to);

            Runtime.Notify(EventKind.ValidatorRemove, Runtime.Chain.Address, from);
            Runtime.Notify(EventKind.ValidatorElect, Runtime.Chain.Address, to);
            Runtime.Notify(EventKind.AddressMigration, to, from);
        }
    }
}
