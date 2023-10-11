using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Core;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Validator.Enums;
using Phantasma.Core.Domain.Contract.Validator.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Business.Blockchain;

public partial class Nexus
{
    #region VALIDATORS
    public Timestamp GetValidatorLastActivity(Address target, Timestamp timestamp)
    {
        var lastActivity = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorLastActivity), target).AsTimestamp();
        return lastActivity;
    }

    public ValidatorEntry[] GetValidators(Timestamp timestamp)
    {
        ValidatorEntry[] validators = null;
        if (this.GetProtocolVersion(RootStorage) > 8)
        {
            validators = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidators)).ToArray<ValidatorEntry>();
        }
        else
        {
            validators = (ValidatorEntry[]) RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidators)).ToObject();
        }
        return validators;
    }

    public int GetPrimaryValidatorCount(Timestamp timestamp)
    {
        var count = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Primary).AsNumber();
        if (count < 1)
        {
            return 1;
        }
        return (int)count;
    }

    public int GetSecondaryValidatorCount(Timestamp timestamp)
    {
        var count = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Primary).AsNumber();
        return (int)count;
    }

    public ValidatorType GetValidatorType(Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorType), address).AsEnum<ValidatorType>();
        return result;
    }

    public bool IsPrimaryValidator(Address address, Timestamp timestamp)
    {
        if (address.IsNull)
        {
            return false;
        }

        if (!HasGenesis())
        {
            return _initialValidators.Contains(address);
        }

        var result = GetValidatorType(address, timestamp);
        return result == ValidatorType.Primary;
    }

    public bool IsSecondaryValidator(Address address, Timestamp timestamp)
    {
        var result = GetValidatorType(address, timestamp);
        return result == ValidatorType.Secondary;
    }

    // this returns true for both active and waiting
    public bool IsKnownValidator(Address address, Timestamp timestamp)
    {
        if (!HasGenesis()) return true;
        var result = GetValidatorType(address, timestamp);
        return result != ValidatorType.Invalid && result != ValidatorType.Proposed;
    }

    public BigInteger GetStakeFromAddress(StorageContext storage, Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetStake), address).AsNumber();
        return result;
    }

    public BigInteger GetUnclaimedFuelFromAddress(StorageContext storage, Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), address).AsNumber();
        return result;
    }

    public Timestamp GetStakeTimestampOfAddress(StorageContext storage, Address address, Timestamp timestamp)
    {
        var result = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetStakeTimestamp), address).AsTimestamp();
        return result;
    }

    public bool IsStakeMaster(StorageContext storage, Address address, Timestamp timestamp)
    {
        var stake = GetStakeFromAddress(storage, address, timestamp);
        if (stake <= 0)
        {
            return false;
        }

        var masterThresold = RootChain.InvokeContractAtTimestamp(storage, timestamp, NativeContractKind.Stake, nameof(StakeContract.GetMasterThreshold)).AsNumber();
        return stake >= masterThresold;
    }

    public int GetIndexOfValidator(Address address, Timestamp timestamp)
    {
        if (!address.IsUser)
        {
            return -1;
        }

        if (RootChain == null)
        {
            return -1;
        }

        var result = (int)RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetIndexOfValidator), address).AsNumber();
        return result;
    }

    public ValidatorEntry GetValidatorByIndex(int index, Timestamp timestamp)
    {
        if (RootChain == null)
        {
            return new ValidatorEntry()
            {
                address = Address.Null,
                election = new Timestamp(0),
                type = ValidatorType.Invalid
            };
        }

        Throw.If(index < 0, "invalid validator index");

        var result = (ValidatorEntry)RootChain.InvokeContractAtTimestamp(this.RootStorage, timestamp, NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorByIndex), (BigInteger)index).ToObject();
        return result;
    }
    
    public ValidatorEntry GetValidator(StorageContext storage, string tAddress)
    {
        if ( !HasGenesis()) 
            return new ValidatorEntry()
            {
                address = Address.Null,
                type = ValidatorType.Invalid,
                election = new Timestamp(0)
            };
        
        var lastBlockHash = this.RootChain.GetLastBlockHash();
        var lastBlock = this.RootChain.GetBlockByHash(lastBlockHash);
        // TODO use builtin methods instead of doing this directly
        /*var validatorEntryVmObject = RootChain.InvokeContractAtTimestamp(storage, lastBlock.Timestamp,
            NativeContractKind.Validator,
            nameof(ValidatorContract.GetCurrentValidator), tAddress);
        return validatorEntryVmObject.AsStruct<ValidatorEntry>();*/
       
        var valueMapKey =  NativeContract.GetKeyForField(NativeContractKind.Validator, "_validators", true);
        var validators = new StorageList(valueMapKey, storage);

        foreach (var validator in validators.All<ValidatorEntry>())
        {
            if (validator.address.TendermintAddress == tAddress)
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

    #endregion
}
