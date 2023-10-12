using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Platform.Structs;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Serilog;

namespace Phantasma.Business.Blockchain;

public partial class Nexus
{
    public int CreatePlatform(StorageContext storage, string externalAddress, Address interopAddress, string name, string fuelSymbol)
    {
        // check if something with this name already exists
        if (PlatformExists(storage, name))
        {
            return -1;
        }

        var platformList = this.GetSystemList(PlatformTag, storage);
        var platformID = (byte)(1 + platformList.Count());

        //var chainAddress = Address.FromHash(name);
        var entry = new PlatformInfo(name, fuelSymbol, new PlatformSwapAddress[] {
            new PlatformSwapAddress() { LocalAddress = interopAddress, ExternalAddress = externalAddress }
        });

        // add to persistent list of tokens
        platformList.Add(name);

        EditPlatform(storage, name, entry);
        // notify oracles on new platform
        this.Notify(storage);
        return platformID;
    }

    private byte[] GetPlatformInfoKey(string name)
    {
        return GetNexusKey($"platform.{name}");
    }

    private void EditPlatform(StorageContext storage, string name, PlatformInfo platformInfo)
    {
        var key = GetPlatformInfoKey(name);
        var bytes = Serialization.Serialize(platformInfo);
        storage.Put(key, bytes);
    }

    public bool PlatformExists(StorageContext storage, string name)
    {
        if (name == DomainSettings.PlatformName)
        {
            return true;
        }

        var key = GetPlatformInfoKey(name);
        return storage.Has(key);
    }

    public PlatformInfo GetPlatformInfo(StorageContext storage, string name)
    {
        var key = GetPlatformInfoKey(name);
        if (storage.Has(key))
        {
            var bytes = storage.Get(key);
            return Serialization.Unserialize<PlatformInfo>(bytes);
        }

        throw new ChainException($"Platform does not exist ({name})");
    }
    
    public void RegisterPlatformAddress(StorageContext storage, string platform, Address localAddress, string externalAddress)
    {
        var platformInfo = GetPlatformInfo(storage, platform);

        foreach (var entry in platformInfo.InteropAddresses)
        {
            Throw.If(entry.LocalAddress == localAddress || entry.ExternalAddress== externalAddress, "address already part of platform interops");
        }

        var newEntry = new PlatformSwapAddress()
        {
            ExternalAddress = externalAddress,
            LocalAddress = localAddress,
        };

        platformInfo.AddAddress(newEntry);
        EditPlatform(storage, platform, platformInfo);
    }

    // TODO optimize this
    public bool IsPlatformAddress(StorageContext storage, Address address)
    {
        if (!address.IsInterop)
        {
            return false;
        }

        var platforms = this.GetPlatforms(storage);
        foreach (var platform in platforms)
        {
            var info = GetPlatformInfo(storage, platform);

            foreach (var entry in info.InteropAddresses)
            {
                if (entry.LocalAddress == address)
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    public bool TokenExistsOnPlatform(string symbol, string platform, StorageContext storage)
    {
        var key = GetNexusKey($"{symbol}.{platform}.hash");
        if (storage.Has(key))
        {
            return true;
        }

        return false;
    }

    public Hash GetTokenPlatformHash(string symbol, string platform, StorageContext storage)
    {
        if (platform == DomainSettings.PlatformName)
        {
            return Hash.FromString(symbol);
        }

        var key = GetNexusKey($"{symbol}.{platform}.hash");
        if (storage.Has(key))
        {
            return storage.Get<Hash>(key);
        }

        return Hash.Null;
    }

    public Hash[] GetPlatformTokenHashes(string platform, StorageContext storage)
    {
        var tokens = GetAvailableTokenSymbols(storage);

        var hashes = new List<Hash>();

        if (platform == DomainSettings.PlatformName)
        {
            foreach (var token in tokens)
            {
                hashes.Add(Hash.FromString(token));
            }
            return hashes.ToArray();
        }

        foreach (var token in tokens)
        {
            var key = GetNexusKey($"{token}.{platform}.hash");
            if (storage.Has(key))
            {
                var tokenHash = storage.Get<Hash>(key);
                if (tokenHash != Hash.Null)
                {
                    hashes.Add(tokenHash);
                }
            }
        }

        return hashes.Distinct().ToArray();
    }
    
    private TokenSwapToSwap GetTokenSwapToSwapFromPlatformAndSymbol(string platform, string symbool, StorageContext storage)
    {
        var key = SmartContract.GetKeyForField(NativeContractKind.Interop, "_PlatformSwappers", true);

        var swappersMap = new StorageMap(key, storage);
        var swapperList = swappersMap.Get<string, StorageList>(platform);
        var swappers = swapperList.All<TokenSwapToSwap>();
        
        foreach( var swapper in swappers)
        {
            if (swapper.Symbol == symbool)
            {
                return swapper;
            }
        }
        return new TokenSwapToSwap();
    }
    
    public Swapper[] GetSwappersForPlatformAndSymbol(string platform, string symbol, StorageContext storage)
    {
        var key = SmartContract.GetKeyForField(NativeContractKind.Interop, "_PlatformSwappers", true);
        var tokenSwapToSwap = GetTokenSwapToSwapFromPlatformAndSymbol(platform, symbol, storage);
        return tokenSwapToSwap.Swappers;
    }
    
    public TokenSwapToSwap[] GetTokensSwapFromPlatform(string platform, StorageContext storage)
    {
        var key = SmartContract.GetKeyForField(NativeContractKind.Interop, "_PlatformSwappers", true);

        var swappers = new StorageMap(key, storage);
        return swappers.Get<string, StorageList>(platform).All<TokenSwapToSwap>();
    }
    

    public string GetPlatformTokenByHashInterop(Hash hash, string platform, StorageContext storage)
    {
        if (hash == Hash.Null)
            return null;
        
        var tokens = GetAvailableTokenSymbols(storage);
        if (platform == DomainSettings.PlatformName)
        {
            foreach (var token in tokens)
            {
                if (Hash.FromString(token) == hash)
                    return token;
            }
        }
        
        var tokensSwapFromPlatform = GetTokensSwapFromPlatform(platform, storage);
        
        foreach (var token in tokens)
        {
            foreach (var externalToken in tokensSwapFromPlatform)
            {
                if ( externalToken.Symbol == token &&
                     Hash.FromString(externalToken.ExternalContractAddress) == hash)
                    return token;
            }
        }
        
        return null;
    }

    public string GetPlatformTokenByHash(Hash hash, string platform, StorageContext storage)
    {
        var tokens = GetAvailableTokenSymbols(storage);

        if (platform == DomainSettings.PlatformName)
        {
            foreach (var token in tokens)
            {
                if (Hash.FromString(token) == hash)
                    return token;
            }
        }

        foreach (var token in tokens)
        {
            var key = GetNexusKey($"{token}.{platform}.hash");
            if (HasTokenPlatformHash(token, platform, storage))
            {
                var tokenHash = storage.Get<Hash>(key);
                if (tokenHash == hash)
                {
                    return token;
                }
            }
        }

        Log.Warning($"Token hash {hash} doesn't exist!");
        return null;
    }

    public void SetPlatformTokenHash(string symbol, string platform, Hash hash, StorageContext storage)
    {
        var tokenKey = GetTokenInfoKey(symbol);
        if (!storage.Has(tokenKey))
        {
            throw new ChainException($"Token does not exist ({symbol})");
        }

        if (platform == DomainSettings.PlatformName)
        {
            throw new ChainException($"cannot set token hash of {symbol} for native platform");
        }

        var bytes = storage.Get(tokenKey);
        var info = Serialization.Unserialize<TokenInfo>(bytes);

        if (!info.Flags.HasFlag(TokenFlags.Swappable))
        {
            info.Flags |= TokenFlags.Swappable;
            EditToken(storage, symbol, info);
        }

        var hashKey = GetNexusKey($"{symbol}.{platform}.hash");

        //should be updateable since a foreign token hash could change
        if (storage.Has(hashKey))
        {
            Log.Warning($"Token hash of {symbol} already set for platform {platform}, updating to {hash}");
        }

        storage.Put<Hash>(hashKey, hash);
    }

    public bool HasTokenPlatformHash(string symbol, string platform, StorageContext storage)
    {
        if (platform == DomainSettings.PlatformName)
        {
            return true;
        }

        var key = GetNexusKey($"{symbol}.{platform}.hash");
        return storage.Has(key);
    }
    
    public string[] GetPlatforms(StorageContext storage)
    {
        var list = GetSystemList(PlatformTag, storage);
        return list.All<string>();
    }
}
