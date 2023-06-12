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
    
    public VMObject InvokeContractAtTimestamp(NativeContractKind nativeContract, string methodName,
        params object[] args)
    {
        return Chain.InvokeContractAtTimestamp(Storage, Time, nativeContract, methodName, args);
    }

    public VMObject InvokeContractAtTimestamp(string contractName, string methodName, params object[] args)
    {
        return Chain.InvokeContractAtTimestamp(Storage, Time, contractName, methodName, args);
    }
    
    public IContract GetContract(string name)
    {
        ExpectNameLength(name, nameof(name));

        throw new NotImplementedException();
    }

    public IContract[] GetContracts()
    {
        return Chain.GetContracts(RootStorage);
    }
    
    public Address GetContractOwner(Address address)
    {
        ExpectAddressSize(address, nameof(address));
        return Chain.GetContractOwner(Storage, address);
    }
    
    public bool ContractExists(string name)
    {
        ExpectNameLength(name, nameof(name));
        return Nexus.ContractExists(RootStorage, name);
    }
    
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
