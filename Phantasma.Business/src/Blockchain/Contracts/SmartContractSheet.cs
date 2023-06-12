using Phantasma.Core.Domain.Contract;

namespace Phantasma.Business.Blockchain.Contracts;

using System;
using System.Numerics;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Utils;

public class SmartContractSheet
{
    public const string ContractListTag = ".contracts.";
    public const string ContractTag = ".contract.";
    public const string OwnerTag = "owner";
    public const string ContractNameTag = "name";
    public const string ContractScriptTag = "script";
    public const string ContractAbiTag = "abi";

    private byte[] _prefix;
    private string _contractName;
    private Address _contractAddress;

    public SmartContractSheet(string contractName)
    {
        _contractName = contractName;
        _contractAddress = SmartContract.GetAddressFromContractName(contractName);
        _prefix = MakePrefix(_contractAddress);
    }

    public SmartContractSheet(Address address)
    {
        _contractName = address.Text;
        _contractAddress = address;
        _prefix = MakePrefix(_contractAddress);
    }


    public SmartContractSheet(string contractName, Address address)
    {
        _contractName = contractName;
        _contractAddress = SmartContract.GetAddressFromContractName(contractName);
        if (address != _contractAddress)
        {
            throw new Exception("Invalid contract address");
        }
        _prefix = MakePrefix(_contractAddress);
    }

    private byte[] GetContractKey(Address contractAddress, string field)
    {
        var bytes = Encoding.ASCII.GetBytes(field);
        var key = ByteArrayUtils.ConcatBytes(_prefix, bytes);
        return key;
    }

    public static byte[] MakePrefix(Address address)
    {
        var key = $"{ContractTag}";
        var firstConcat = ByteArrayUtils.ConcatBytes(Encoding.UTF8.GetBytes(key), address.ToByteArray());
        return ByteArrayUtils.ConcatBytes(firstConcat, Encoding.UTF8.GetBytes("."));
    }

    #region Has
    public bool Has(StorageContext storage, byte[] key)
    {
        return storage.Has(key);
    }

    public bool HasScript(StorageContext storage)
    {
        return storage.Has(GetScriptKey());
    }

    public bool HasOwner(StorageContext storage)
    {
        return storage.Has(GetOwnerKey());
    }

    public bool HasABI(StorageContext storage)
    {
        return storage.Has(GetABIKey());
    }

    public bool HasName(StorageContext storage)
    {
        return storage.Has(GetNameKey());
    }
    #endregion

    #region Put
    public bool Put(StorageContext storage, byte[] key, byte[] value)
    {
        lock (storage)
        {
            storage.Put(key, value);
            return true;
        }

        return false;
    }

    public bool PutScript(StorageContext storage, byte[] value)
    {
        return Put(storage, GetScriptKey(), value);
    }

    public bool PutOwner(StorageContext storage, byte[] value)
    {
        return Put(storage, GetOwnerKey(), value);
    }

    public bool PutABI(StorageContext storage, byte[] value)
    {
        return Put(storage, GetABIKey(), value);
    }

    public bool PutName(StorageContext storage, byte[] value)
    {
        return Put(storage, GetNameKey(), value);
    }
    #endregion

    #region Add
    public void AddToList(StorageContext storage, Address address)
    {
        var contracts = GetContractList(storage);
        contracts.Add(address);
    }
    #endregion

    #region Delete
    public void Delete(StorageContext storage, byte[] key)
    {
        storage.Delete(key);
    }

    public void DeleteABI(StorageContext storage)
    {
        storage.Delete(GetABIKey());
    }

    public void DeleteScript(StorageContext storage)
    {
        storage.Delete(GetScriptKey());
    }

    public void DeleteOwner(StorageContext storage)
    {
        storage.Delete(GetOwnerKey());
    }

    public void DeleteName(StorageContext storage)
    {
        storage.Delete(GetNameKey());
    }

    public void DeleteContract(StorageContext storage)
    {
        storage.Visit((key, val) =>
        {
            storage.Delete(key);
        }, storage.Count(), _prefix);
    }
    #endregion

    #region Get
    public static byte[] GetContractListKey()
    {
        return Encoding.ASCII.GetBytes(".contracts.");
    }

    private byte[] GetKeyForAddress(Address address)
    {
        return ByteArrayUtils.ConcatBytes(_prefix, address.ToByteArray());
    }

    public byte[] GetOwnerKey()
    {
        return GetContractKey(_contractAddress, OwnerTag);
    }

    public byte[] GetABIKey()
    {
        return GetContractKey(_contractAddress, ContractAbiTag);
    }

    public byte[] GetABI(StorageContext storage)
    {
        return storage.Get(GetABIKey());
    }

    public byte[] GetNameKey()
    {
        return GetContractKey(_contractAddress, ContractNameTag);
    }

    public byte[] GetName(StorageContext storage)
    {
        return storage.Get(GetNameKey());
    }

    public byte[] GetScriptKey()
    {
        return GetContractKey(_contractAddress, ContractScriptTag);
    }

    public byte[] GetScript(StorageContext storage)
    {
        return storage.Get(GetScriptKey());
    }

    public StorageList GetContractList(StorageContext storage)
    {
        return new StorageList(GetContractListKey(), storage);
    }

    public Address GetOwner(StorageContext storage)
    {
        var key = GetOwnerKey();
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            return Address.FromBytes(bytes);
        }
        return Address.Null;
    }
    #endregion
}

