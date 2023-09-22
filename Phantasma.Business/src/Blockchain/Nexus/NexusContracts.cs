using System;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Storage.Context;

namespace Phantasma.Business.Blockchain;

public partial class Nexus
{
    #region CONTRACTS
    public SmartContract GetContractByName(StorageContext storage, string contractName)
    {
        Throw.IfNullOrEmpty(contractName, nameof(contractName));

        if (ValidationUtils.IsValidTicker(contractName))
        {
            var tokenInfo = GetTokenInfo(storage, contractName);
            return new CustomContract(contractName, tokenInfo.Script, tokenInfo.ABI);
        }

        var address = SmartContract.GetAddressFromContractName(contractName);
        SmartContract result = NativeContract.GetNativeContractByAddress(address);

        if (result == null)
        {
            result = RootChain.GetContractByAddress(storage, address);
        }

        return result;
    }

    #endregion
    
    #region Contracts
    private byte[] GetContractInfoKey(string name)
    {
        return GetNexusKey($"contract.{name}");
    }

    /*
    private void EditContract(StorageContext storage, string name, PlatformInfo platformInfo)
    {
        var key = GetPlatformInfoKey(name);
        var bytes = Serialization.Serialize(platformInfo);
        storage.Put(key, bytes);
    }*/

    public static bool IsNativeContract(string name)
    {
        NativeContractKind kind;
        return Enum.TryParse<NativeContractKind>(name, true, out kind);
    }

    public bool ContractExists(StorageContext storage, string name)
    {
        if (IsNativeContract(name))
        {
            return true;
        }

        var key = GetContractInfoKey(name);
        return storage.Has(key);
    }

    /*
    public PlatformInfo GetPlatformInfo(StorageContext storage, string name)
    {
        var key = GetPlatformInfoKey(name);
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            return Serialization.Unserialize<PlatformInfo>(bytes);
        }

        throw new ChainException($"Platform does not exist ({name})");
    }*/
    #endregion
}
