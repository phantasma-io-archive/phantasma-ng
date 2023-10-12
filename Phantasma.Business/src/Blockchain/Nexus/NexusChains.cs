using System;
using System.Collections.Generic;
using System.Text;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;

namespace Phantasma.Business.Blockchain;

public partial class Nexus
{
    #region CHAINS
    public bool CreateChain(StorageContext storage, string organization, string name, string parentChainName)
    {
        if (name != DomainSettings.RootChainName)
        {
            if (string.IsNullOrEmpty(parentChainName))
            {
                return false;
            }

            if (!ChainExists(storage, parentChainName))
            {
                return false;
            }
        }

        if (!ValidationUtils.IsValidIdentifier(name))
        {
            return false;
        }

        // check if already exists something with that name
        if (ChainExists(storage, name))
        {
            return false;
        }

        if (PlatformExists(storage, name))
        {
            return false;
        }

        var chain = _instantiateChain(this, name);

        // add to persistent list of chains
        var chainList = this.GetSystemList(ChainTag, storage);
        chainList.Add(name);

        // add address and name mapping
        storage.Put(ChainNameMapKey + chain.Name, chain.Address.ToByteArray());
        storage.Put(ChainAddressMapKey + chain.Address.Text, Encoding.UTF8.GetBytes(chain.Name));
        storage.Put(ChainOrgKey + chain.Name, Encoding.UTF8.GetBytes(organization));

        if (!string.IsNullOrEmpty(parentChainName))
        {
            storage.Put(ChainParentNameKey + chain.Name, Encoding.UTF8.GetBytes(parentChainName));
            var childrenList = GetChildrenListOfChain(storage, parentChainName);
            childrenList.Add<string>(chain.Name);
        }

        _chainCache[chain.Name] = chain;

        return true;
    }

    public string LookUpChainNameByAddress(Address address)
    {
        var key = ChainAddressMapKey + address.Text;
        if (RootStorage.Has(key))
        {
            var bytes = RootStorage.Get(key);
            return Encoding.UTF8.GetString(bytes);
        }

        return null;
    }

    public bool ChainExists(StorageContext storage, string chainName)
    {
        if (string.IsNullOrEmpty(chainName))
        {
            return false;
        }

        var key = ChainNameMapKey + chainName;
        return storage.Has(key);
    }

    private Dictionary<string, IChain> _chainCache = new Dictionary<string, IChain>();

    public string GetParentChainByAddress(Address address)
    {
        var chain = GetChainByAddress(address);
        if (chain == null)
        {
            return null;
        }
        return GetParentChainByName(chain.Name);
    }

    public string GetParentChainByName(string chainName)
    {
        if (chainName == DomainSettings.RootChainName)
        {
            return null;
        }

        var key = ChainParentNameKey + chainName;
        if (RootStorage.Has(key))
        {
            var bytes = RootStorage.Get(key);
            var parentName = Encoding.UTF8.GetString(bytes);
            return parentName;
        }

        throw new Exception("Parent name not found for chain: " + chainName);
    }

    public string GetChainOrganization(string chainName)
    {
        var key = ChainOrgKey + chainName;
        if (RootStorage.Has(key))
        {
            var bytes = RootStorage.Get(key);
            var orgName = Encoding.UTF8.GetString(bytes);
            return orgName;
        }

        return null;
    }

    public IEnumerable<string> GetChildChainsByAddress(StorageContext storage, Address chainAddress)
    {
        var chain = GetChainByAddress(chainAddress);
        if (chain == null)
        {
            return null;
        }

        return GetChildChainsByName(storage, chain.Name);
    }

    public IOracleReader GetOracleReader()
    {
        Throw.If(_oracleReader == null, "Oracle reader has not been set yet.");
        return _oracleReader;
    }

    public IEnumerable<string> GetChildChainsByName(StorageContext storage, string chainName)
    {
        var list = GetChildrenListOfChain(storage, chainName);
        var count = (int)list.Count();
        var names = new string[count];
        for (int i = 0; i < count; i++)
        {
            names[i] = list.Get<string>(i);
        }

        return names;
    }

    private StorageList GetChildrenListOfChain(StorageContext storage, string chainName)
    {
        var key = Encoding.UTF8.GetBytes(ChainChildrenBlockKey + chainName);
        var list = new StorageList(key, storage);
        return list;
    }

    public IChain GetChainByAddress(Address address)
    {
        var name = LookUpChainNameByAddress(address);
        if (string.IsNullOrEmpty(name))
        {
            return null; // TODO should be exception
        }

        return GetChainByName(name);
    }

    public IChain GetChainByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (_chainCache.ContainsKey(name))
        {
            return _chainCache[name];
        }

        if (ChainExists(RootStorage, name))
        {
            var chain = _instantiateChain(this, name);
            _chainCache[name] = chain;
            return chain;
        }

        //throw new Exception("Chain not found: " + name);
        return null;
    }
    
    public int GetIndexOfChain(string name)
    {
        var chains = this.GetChains(RootStorage);
        int index = 0;
        foreach (var chain in chains)
        {
            if (chain == name)
            {
                return index;
            }

            index++;
        }
        return -1;
    }
    
    
    public string[] GetChains(StorageContext storage)
    {
        var list = GetSystemList(ChainTag, storage);
        return list.All<string>();
    }
    
    #endregion
}
