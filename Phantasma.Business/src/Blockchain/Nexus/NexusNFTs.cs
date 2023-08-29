using System;
using System.Collections.Generic;
using System.Numerics;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Core;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types.Structs;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain;

public partial class Nexus
{
    #region NFT

    public byte[] GetKeyForNFT(string symbol, BigInteger tokenID)
    {
        return GetKeyForNFT(symbol, tokenID.ToString());
    }

    public byte[] GetKeyForNFT(string symbol, string key)
    {
        var tokenKey = SmartContract.GetKeyForField(symbol, key, false);
        return tokenKey;
    }

    private StorageList GetSeriesList(StorageContext storage, string symbol)
    {
        var key = System.Text.Encoding.ASCII.GetBytes("series." + symbol);
        return new StorageList(key, storage);
    }

    public BigInteger[] GetAllSeriesForToken(StorageContext storage, string symbol)
    {
        var list = GetSeriesList(storage, symbol);
        return list.All<BigInteger>();
    }

    public TokenSeries CreateSeries(StorageContext storage, IToken token, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi)
    {
        if (token.IsFungible())
        {
            throw new ChainException($"Can't create series for fungible token");
        }

        var key = GetTokenSeriesKey(token.Symbol, seriesID);

        if (storage.Has(key))
        {
            throw new ChainException($"Series {seriesID} of token {token.Symbol} already exist");
        }

        if (token.IsCapped() && maxSupply < 1)
        {
            throw new ChainException($"Token series supply must be 1 or more");
        }

        var nftStandard = Tokens.TokenUtils.GetNFTStandard();

        if (!abi.Implements(nftStandard))
        {
            throw new ChainException($"Token series abi does not implement the NFT standard");
        }

        var series = new TokenSeries(0, maxSupply, mode, script, abi, null);
        WriteTokenSeries(storage, token.Symbol, seriesID, series);

        var list = GetSeriesList(storage, token.Symbol);
        list.Add(seriesID);

        return series;
    }

    public byte[] GetTokenSeriesKey(string symbol, BigInteger seriesID)
    {
        return GetKeyForNFT(symbol, $"serie{seriesID}");
    }

    public TokenSeries GetTokenSeries(StorageContext storage, string symbol, BigInteger seriesID)
    {
        var key = GetTokenSeriesKey(symbol, seriesID);

        if (storage.Has(key))
        {
            return storage.Get<TokenSeries>(key);
        }

        return null;
    }

    private void WriteTokenSeries(StorageContext storage, string symbol, BigInteger seriesID, ITokenSeries series)
    {
        var key = GetTokenSeriesKey(symbol, seriesID);
        storage.Put<TokenSeries>(key, (TokenSeries)series);
    }

    public BigInteger GenerateNFT(IRuntime Runtime, string symbol, string chainName, Address targetAddress, byte[] rom, byte[] ram, BigInteger seriesID)
    {
        Runtime.Expect(ram != null, "invalid nft ram");

        Runtime.Expect(seriesID >= 0, "invalid series ID");

        var series = GetTokenSeries(Runtime.RootStorage, symbol, seriesID);
        Runtime.Expect(series != null, $"{symbol} series {seriesID} does not exist");

        BigInteger mintID = series.GenerateMintID();
        Runtime.Expect(mintID > 0, "invalid mintID generated");

        if (Runtime.ProtocolVersion >= 13)
        {
            Runtime.Expect(Runtime.Transaction.Signatures.Length > 0, "No signatures found in transaction");
        }

        if (series.Mode == TokenSeriesMode.Duplicated)
        {
            if (mintID > 1)
            {
                if (rom == null || rom.Length == 0)
                {
                    rom = series.ROM;
                }
                else
                {
                    Runtime.Expect(ByteArrayUtils.CompareBytes(rom, series.ROM), $"rom can't be unique in {symbol} series {seriesID}");
                }
            }
            else
            {
                series.SetROM(rom);
            }

            rom = new byte[0];
        }
        else
        {
            Runtime.Expect(rom != null && rom.Length > 0, "invalid nft rom");
        }

        WriteTokenSeries(Runtime.RootStorage, symbol, seriesID, series);

        var token = Runtime.GetToken(symbol);

        if (series.MaxSupply > 0)
        {
            Runtime.Expect(mintID <= series.MaxSupply, $"{symbol} series {seriesID} reached max supply already");
        }
        else
        {
            Runtime.Expect(!token.IsCapped(), $"{symbol} series {seriesID} max supply is not defined yet");
        }

        var content = new TokenContent(seriesID, mintID, chainName, targetAddress, targetAddress, rom, ram, Runtime.Time, null, series.Mode);

        var tokenKey = GetKeyForNFT(symbol, content.TokenID);
        Runtime.Expect(!Runtime.Storage.Has(tokenKey), "duplicated nft");

        var contractAddress = token.GetContractAddress();

        var bytes = content.ToByteArray();
        bytes = CompressionUtils.Compress(bytes);

        if (Runtime.ProtocolVersion <= 12)
        {
            Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, tokenKey, bytes);
        }
        else
        {
            Runtime.WriteData(contractAddress, tokenKey, bytes);
        }
        
        return content.TokenID;
    }

    private Address _infusionOperationAddress = Address.Null;

    private void DoInfusionOperation(Address targetAdress, Action callback)
    {
        _infusionOperationAddress = targetAdress;
        callback();
        _infusionOperationAddress = Address.Null;
    }


    public void DestroyNFT(IRuntime Runtime, string symbol, BigInteger tokenID, Address target)
    {
        var infusionAddress = DomainSettings.InfusionAddress;

        var tokenContent = ReadNFT(Runtime, symbol, tokenID);

        if (Runtime.ProtocolVersion >= 13)
        {
            Runtime.Expect(Runtime.Transaction.Signatures.Length > 0, "No signatures found in transaction");
        }

        foreach (var asset in tokenContent.Infusion)
        {
            var assetInfo = this.GetTokenInfo(Runtime.RootStorage, asset.Symbol);

#if ALLOWANCE_OPERATIONS
            Runtime.AddAllowance(infusionAddress, asset.Symbol, asset.Value);
#endif

            DoInfusionOperation(target, () =>
            {
                if (assetInfo.IsFungible())
                {
                    Runtime.CheckFilterAmountThreshold(assetInfo, target, asset.Value, "Burn Token (DestroyNFT)");
                    this.TransferTokens(Runtime, assetInfo, infusionAddress, target, asset.Value, true);
                }
                else
                {
                    this.TransferToken(Runtime, assetInfo, infusionAddress, target, asset.Value, true);
                }
            });

#if ALLOWANCE_OPERATIONS
            Runtime.RemoveAllowance(infusionAddress, asset.Symbol);
#endif
        }

        var token = Runtime.GetToken(symbol);
        var contractAddress = token.GetContractAddress();

        var tokenKey = GetKeyForNFT(symbol, tokenID);

        if (Runtime.ProtocolVersion <= 12)
            Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.DeleteData), contractAddress, tokenKey);
        else
            Runtime.DeleteData(contractAddress, tokenKey);
    }

    public void WriteNFT(IRuntime Runtime, string symbol, BigInteger tokenID, string chainName, Address creator,
            Address owner, byte[] rom, byte[] ram, BigInteger seriesID, Timestamp timestamp,
            IEnumerable<TokenInfusion> infusion, bool mustExist)
    {
        Runtime.Expect(ram != null && ram.Length < TokenContent.MaxRAMSize, "invalid nft ram update");

        var tokenKey = GetKeyForNFT(symbol, tokenID);

        if (Runtime.ProtocolVersion >= 13)
        {
            Runtime.Expect(Runtime.Transaction.Signatures.Length > 0, "No signatures found in transaction");
        }

        if (Runtime.RootStorage.Has(tokenKey))
        {
            var content = ReadNFTRaw(Runtime.RootStorage, tokenKey, Runtime.ProtocolVersion);

            var series = GetTokenSeries(Runtime.RootStorage, symbol, content.SeriesID);
            Runtime.Expect(series != null, $"could not find series {seriesID} for {symbol}");

            switch (series.Mode)
            {
                case TokenSeriesMode.Unique:
                    Runtime.Expect(rom.CompareBytes(content.ROM), "rom does not match original value");
                    break;

                case TokenSeriesMode.Duplicated:
                    Runtime.Expect(rom.Length == 0 || rom.CompareBytes(series.ROM), "rom does not match original value");
                    break;

                default:
                    throw new ChainException("WriteNFT: unsupported series mode: " + series.Mode);
            }

            content = new TokenContent(content.SeriesID, content.MintID, chainName, content.Creator, owner, content.ROM, ram, timestamp, infusion, series.Mode);

            var token = Runtime.GetToken(symbol);
            var contractAddress = token.GetContractAddress();

            var bytes = content.ToByteArray();
            bytes = CompressionUtils.Compress(bytes);

            if ( Runtime.ProtocolVersion <= 12)
                Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, tokenKey, bytes);
            else
                Runtime.WriteData(contractAddress, tokenKey, bytes);
        }
        else
        {
            Runtime.Expect(!mustExist, $"nft {symbol} {tokenID} does not exist");
            Address _creator = creator;

            var genID = GenerateNFT(Runtime, symbol, chainName, _creator, rom, ram, seriesID);
            Runtime.Expect(genID == tokenID, "failed to regenerate NFT");
        }
    }

    public TokenContent ReadNFT(IRuntime Runtime, string symbol, BigInteger tokenID)
    {
        return ReadNFT(Runtime.RootStorage, symbol, tokenID, Runtime.ProtocolVersion);
    }

    public TokenContent ReadNFT(StorageContext storage, string symbol, BigInteger tokenID)
    {
        var protocol = this.GetProtocolVersion(storage);
        return ReadNFT(storage, symbol, tokenID, protocol);
    }

    private TokenContent ReadNFTRaw(StorageContext storage, byte[] tokenKey, uint ProtocolVersion)
    {
        var bytes = storage.Get(tokenKey);

        bytes = CompressionUtils.Decompress(bytes);

        var content = Serialization.Unserialize<TokenContent>(bytes);
        return content;
    }

    private TokenContent ReadNFT(StorageContext storage, string symbol, BigInteger tokenID, uint ProtocolVersion)
    {
        var tokenKey = GetKeyForNFT(symbol, tokenID);

        Throw.If(!storage.Has(tokenKey), $"nft {symbol} {tokenID} does not exist");

        var content = ReadNFTRaw(storage, tokenKey, ProtocolVersion);

        var series = GetTokenSeries(storage, symbol, content.SeriesID);

        content.UpdateTokenID(series.Mode);

        if (series.Mode == TokenSeriesMode.Duplicated)
        {
            content.ReplaceROM(series.ROM);
        }
        return content;
    }

    public bool HasNFT(StorageContext storage, string symbol, BigInteger tokenID)
    {
        var tokenKey = GetKeyForNFT(symbol, tokenID);
        return storage.Has(tokenKey);
    }
    #endregion

}
