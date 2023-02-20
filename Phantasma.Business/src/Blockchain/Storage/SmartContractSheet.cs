using System;
using System.Numerics;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Storage;

public class SmartContractSheet
{
    public const string ContractListTag = ".contracts.";
    public const string ContractTag = ".contract.";
    public const string OwnerTag = ".owner";
    public const string ContractNameTag = ".name";
    public const string ContractScriptTag = ".script.";
    public const string ContractAbiTag = ".abi.";
    
    private byte[] _prefix;
    private string _contractName;
    private Address _contractAddress;
    
    public SmartContractSheet(string contractName)
    {
        this._contractName = contractName;
        this._contractAddress = SmartContract.GetAddressFromContractName(contractName);
        this._prefix = MakePrefix(_contractAddress);
    }
    
    public SmartContractSheet(Address address)
    {
        this._contractName = address.Text;
        this._contractAddress = address;
        this._prefix = MakePrefix(_contractAddress);
    }

    
    public SmartContractSheet(string contractName, Address address)
    {
        this._contractName = contractName;
        this._contractAddress = SmartContract.GetAddressFromContractName(contractName);
        if (address != _contractAddress)
        { 
            throw new Exception("Invalid contract address");
        }
        this._prefix = MakePrefix(_contractAddress);
    }
    
    private byte[] GetContractKey(Address contractAddress, string field)
    {
        var bytes = Encoding.ASCII.GetBytes(field);
        var key = ByteArrayUtils.ConcatBytes(_prefix, bytes);
        return key;
    }

    public static byte[] MakePrefix(Address address)
    {
        var key = $".{ContractTag}.";
        return ByteArrayUtils.ConcatBytes(Encoding.UTF8.GetBytes(key), address.ToByteArray());
    }

    #region Has
    public bool Has(StorageContext storage, byte[] key)
    {
        return storage.Has(key);
    }
    
    public bool HasScript(StorageContext storage)
    {
        return storage.Has(this.GetScriptKey());
    }
    
    public bool HasOwner(StorageContext storage)
    {
        return storage.Has(this.GetOwnerKey());
    }
    
    public bool HasABI(StorageContext storage)
    {
        return storage.Has(this.GetABIKey());
    }
    
    public bool HasName(StorageContext storage)
    {
        return storage.Has(this.GetNameKey());
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
        return Put(storage, this.GetScriptKey(), value);
    }
    
    public bool PutOwner(StorageContext storage, byte[] value)
    {
        return Put(storage, this.GetOwnerKey(), value);
    }
    
    public bool PutABI(StorageContext storage, byte[] value)
    {
        return Put(storage, this.GetABIKey(), value);
    }
    
    public bool PutName(StorageContext storage, byte[] value)
    {
        return Put(storage, this.GetNameKey(), value);
    }
    #endregion
    
    #region Add
    public void Add(StorageContext storage, Address address)
    {
        var contracts = GetContractList(storage);
        contracts.Add<Address>(address);
    }
    #endregion
    
    #region Delete
    public void Delete(StorageContext storage, byte[] key)
    {
        storage.Delete(key);
    }
    
    public void DeleteABI(StorageContext storage)
    {
        storage.Delete(this.GetABIKey());
    }
    
    public void DeleteScript(StorageContext storage)
    {
        storage.Delete(this.GetScriptKey());
    }
    
    public void DeleteOwner(StorageContext storage)
    {
        storage.Delete(this.GetOwnerKey());
    }
    
    public void DeleteName(StorageContext storage)
    {
        storage.Delete(this.GetNameKey());
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
        return GetContractKey(this._contractAddress, OwnerTag);
    }
    
    public byte[] GetABIKey()
    {
        return GetContractKey(this._contractAddress, ContractAbiTag);
    }
    
    public byte[] GetABI(StorageContext storage)
    {
        return storage.Get(this.GetABIKey());
    }
    
    public byte[] GetNameKey()
    {
        return GetContractKey(this._contractAddress, ContractNameTag);
    }
    
    public byte[] GetName(StorageContext storage)
    {
        return storage.Get(this.GetNameKey());
    }
    
    public byte[] GetScriptKey()
    {
        return GetContractKey(this._contractAddress, ContractScriptTag);
    }

    public byte[] GetScript(StorageContext storage)
    {
        return storage.Get(this.GetScriptKey());
    }
    
    public StorageList GetContractList(StorageContext storage)
    {
        return new StorageList(GetContractListKey(), storage);
    }
    
    public Address GetOwner(StorageContext storage)
    {
        var key = this.GetOwnerKey();
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            return Address.FromBytes(bytes);
        }
        return Address.Null;
    }
    #endregion
}
