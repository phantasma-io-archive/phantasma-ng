using System;
using System.Text;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;

namespace Phantasma.Business.Blockchain.VM;

public partial class RuntimeVM : GasMachine, IRuntime 
{
    /// <summary>
    /// Check if is a minting address
    /// </summary>
    /// <param name="address"></param>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public bool IsMintingAddress(Address address, string symbol)
    {
        ExpectAddressSize(address, nameof(address));
        ExpectNameLength(symbol, nameof(symbol));

        if (TokenExists(symbol))
        {
            var info = GetToken(symbol);

            if (address == info.Owner)
            {
                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Invoke Contract at Timestamp (native contract kind)
    /// </summary>
    /// <param name="nativeContract"></param>
    /// <param name="methodName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public VMObject InvokeContractAtTimestamp(NativeContractKind nativeContract, string methodName,
        params object[] args)
    {
        return Chain.InvokeContractAtTimestamp(Storage, Time, nativeContract, methodName, args);
    }

    /// <summary>
    /// Invoke Contract at Timestamp
    /// </summary>
    /// <param name="contractName"></param>
    /// <param name="methodName"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public VMObject InvokeContractAtTimestamp(string contractName, string methodName, params object[] args)
    {
        return Chain.InvokeContractAtTimestamp(Storage, Time, contractName, methodName, args);
    }
    
    /// <summary>
    /// Get Contract by name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public IContract GetContract(string name)
    {
        ExpectNameLength(name, nameof(name));

        throw new NotImplementedException();
    }

    /// <summary>
    /// Get all the contracts
    /// </summary>
    /// <returns></returns>
    public IContract[] GetContracts()
    {
        return Chain.GetContracts(RootStorage);
    }
    
    /// <summary>
    /// Get contract owner
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public Address GetContractOwner(Address address)
    {
        ExpectAddressSize(address, nameof(address));
        return Chain.GetContractOwner(Storage, address);
    }
    
    /// <summary>
    /// Check if a contract exists
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public bool ContractExists(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.ContractExists(RootStorage, name);
    }
    
    /// <summary>
    /// Check if contract is deployed
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public bool ContractDeployed(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Chain.IsContractDeployed(Storage, name);
    }
    
    private ContractInterface OptimizedAddressABILookup(Address target)
    {
        if (_optimizedABIMapKey == null)
        {
            var accountContractName = NativeContractKind.Account.GetContractName();
            _optimizedABIMapKey = Encoding.UTF8.GetBytes($".{accountContractName}._abiMap");
        }

        var abiMap = new StorageMap(_optimizedABIMapKey, RootStorage);

        if (abiMap.ContainsKey(target))
        {
            var bytes = abiMap.Get<Address, byte[]>(target);
            return ContractInterface.FromBytes(bytes);
        }
        else
            return null;

    }
}
