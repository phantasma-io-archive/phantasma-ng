using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Neo;
using Nethereum.RPC.Eth.DTOs;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.API;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Node.Chains.Neo2;
using Phantasma.Node.Interop;
using Serilog;
using NeoBlock = Neo.Network.P2P.Payloads.Block;
using NeoTx = Neo.Network.P2P.Payloads.Transaction;

namespace Phantasma.Node.Oracles
{
    public class SpookOracle : OracleReader, IOracleObserver
    {
        private readonly Node _cli;

        private Dictionary<string, IKeyValueStoreAdapter> _keystoreCache = new Dictionary<string, IKeyValueStoreAdapter>();
        private Dictionary<string, CachedFee> _feeCache = new Dictionary<string, CachedFee>();
        private Dictionary<string, object> _keyValueStore = new Dictionary<string, object>();
        private KeyValueStore<string, string> platforms;

        enum StorageConst
        {
            CurrentHeight,
            Block,
            Transaction,
            Platform
        }

        public SpookOracle(Node cli, Nexus nexus) : base(nexus)
        {
            this._cli = cli;
            nexus.Attach(this);
            platforms = new KeyValueStore<string, string>(CreateKeyStoreAdapter(StorageConst.Platform.ToString()));

            Log.Information("Platform count: " + platforms.Count);

            var nexusPlatforms = (nexus as Nexus).GetPlatforms(nexus.RootStorage);
            foreach (var nexusPlatform in nexusPlatforms)
            {
                if (!platforms.ContainsKey(nexusPlatform))
                {
                    platforms.Set(nexusPlatform, nexusPlatform);
                }

                _keyValueStore.Add(nexusPlatform + StorageConst.Block, new KeyValueStore<string, InteropBlock>(CreateKeyStoreAdapter(nexusPlatform + StorageConst.Block)));
                _keyValueStore.Add(nexusPlatform + StorageConst.Transaction, new KeyValueStore<string, InteropTransaction>(CreateKeyStoreAdapter(nexusPlatform + StorageConst.Transaction)));
                _keyValueStore.Add(nexusPlatform + StorageConst.CurrentHeight, new KeyValueStore<string, string>(CreateKeyStoreAdapter(nexusPlatform + StorageConst.CurrentHeight)));
            }
        }

        public void Update(INexus nexus, StorageContext storage)
        {
            var nexusPlatforms = (nexus as Nexus).GetPlatforms(storage);
            foreach (var platform in nexusPlatforms)
            {
                if (_keyValueStore.ContainsKey(platform + StorageConst.Block) || _keyValueStore.ContainsKey(platform + StorageConst.Transaction))
                {
                    continue;
                }
                platforms.Set(platform, platform);

                _keyValueStore.Add(platform + StorageConst.Block, new KeyValueStore<string, InteropBlock>(CreateKeyStoreAdapter(platform + StorageConst.Block)));
                _keyValueStore.Add(platform + StorageConst.Transaction, new KeyValueStore<string, InteropTransaction>(CreateKeyStoreAdapter(platform + StorageConst.Transaction)));
                _keyValueStore.Add(platform + StorageConst.CurrentHeight, new KeyValueStore<string, string>(CreateKeyStoreAdapter(platform + StorageConst.CurrentHeight)));
            }
        }

        private IKeyValueStoreAdapter CreateKeyStoreAdapter(string name)
        {
            if (_keystoreCache.ContainsKey(name))
            {
                return _keystoreCache[name];
            }

            IKeyValueStoreAdapter result = Nexus.CreateKeyStoreAdapter(name);
            _keystoreCache[name] = result;

            return result;
        }

        private T Read<T>(string platform, string chainName, Hash hash, StorageConst type)
        {
            var storageKey = type + chainName + hash.ToString();
            var keyStore = _keyValueStore[platform + type] as KeyValueStore<string, T>;

            try
            {
                if(keyStore.TryGet(storageKey, out T data))
                {
                    return data;
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return default(T);
            }
            return default(T);
        }

        public override List<InteropBlock> ReadAllBlocks(string platformName, string chainName)
        {
            var blockList = new List<InteropBlock>();
            var keyStore = _keyValueStore[platformName + StorageConst.Block] as KeyValueStore<string, InteropBlock>;

            keyStore.Visit((key, value) =>
    		{
                blockList.Add(value);
    		});

    		return blockList;
        }

        public override string GetCurrentHeight(string platformName, string chainName)
        {
            var storageKey = StorageConst.CurrentHeight + platformName + chainName;
            var keyStore = _keyValueStore[platformName + StorageConst.CurrentHeight] as KeyValueStore<string, string>;
            if (keyStore.TryGet(storageKey, out string height))
            {
                return height; 
            }

            return "";
        }

        public override void SetCurrentHeight(string platformName, string chainName, string height)
        {
            var storageKey = StorageConst.CurrentHeight + platformName + chainName;
            var keyStore = _keyValueStore[platformName + StorageConst.CurrentHeight] as KeyValueStore<string, string>;

            keyStore.Set(storageKey, height);
        }

        private bool Persist<T>(string platform, string chainName, Hash hash, StorageConst type, T data)
        {
            var storageKey = type + chainName + hash.ToString();
            var keyStore = _keyValueStore[platform + type] as KeyValueStore<string, T>;

            if(!keyStore.ContainsKey(storageKey))
            {
                keyStore.Set(storageKey, data);
                return true;
            }

            keyStore.Set(storageKey, data);
            Log.Error("storageKey " + storageKey + " updated!");
            return false;
        }

        protected override BigInteger PullFee(Timestamp time, string platform)
        {
            platform = platform.ToLower();

            switch (platform)
            {
                case NeoWallet.NeoPlatform:
                    return UnitConversion.ToBigInteger(0.1m, DomainSettings.FiatTokenDecimals);

                case EthereumWallet.EthereumPlatform:

                    CachedFee fee;
                    if (_feeCache.TryGetValue(platform, out fee))
                    {
                        if ((time - fee.Time) < 60)
                        {
                            var logMessage = $"PullFee({platform}): Cached fee pulled: {fee.Value}, GAS limit: {Settings.Instance.Oracle.EthGasLimit}, calculated fee: {fee.Value * Settings.Instance.Oracle.EthGasLimit}";
                            Log.Debug(logMessage);

                            return fee.Value * Settings.Instance.Oracle.EthGasLimit;
                        }
                    }

                    var newFee = EthereumInterop.GetNormalizedFee(Settings.Instance.Oracle.EthFeeURLs.ToArray());
                    fee = new CachedFee(time, UnitConversion.ToBigInteger(newFee, 9)); // 9 for GWEI
                    _feeCache[platform] = fee;

                    var logMessage2 = $"PullFee({platform}): New fee pulled: {fee.Value}, GAS limit: {Settings.Instance.Oracle.EthGasLimit}, calculated fee: {fee.Value * Settings.Instance.Oracle.EthGasLimit}";
                    Log.Debug(logMessage2);

                    return fee.Value * Settings.Instance.Oracle.EthGasLimit;

                default:
                    throw new OracleException($"Support for {platform} fee not implemented in this node");
            }
        }

        protected override decimal PullPrice(Timestamp time, string symbol)
        {
            var apiKey = _cli.CryptoCompareAPIKey;
            var pricerCGEnabled = Settings.Instance.Oracle.PricerCoinGeckoEnabled;
            var pricerSupportedTokens = Settings.Instance.Oracle.PricerSupportedTokens.ToArray();
            
            if (time <= new Timestamp(1644570000))
            {
                if (symbol == DomainSettings.FuelTokenSymbol)
                {
                    var result = PullPrice(time, DomainSettings.StakingTokenSymbol);
                    return result / 5;
                }
            }

            var price = Pricer.GetCoinRateWithTime(time, symbol, DomainSettings.FiatTokenSymbol, apiKey, pricerCGEnabled, pricerSupportedTokens);
            return price;
        }

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, BigInteger height = new BigInteger())
        {
            if (hash == Hash.Null && height == BigInteger.Zero)
            {
                throw new OracleException($"Fetching block not possible without hash or height");
            }

            InteropBlock block = Read<InteropBlock>(platformName, chainName, hash, StorageConst.Block);

            if (height == BigInteger.Zero && block.Hash != Hash.Null)
            {
                return block;
            }

            var nexus = NexusAPI.GetNexus();
            var tokenSwapper = NexusAPI.GetTokenSwapper();

            Tuple<InteropBlock, InteropTransaction[]> interopTuple;
            switch (platformName)
            {
                case NeoWallet.NeoPlatform:

                    NeoBlock neoBlock;

                    if (height == 0)
                    {
                        neoBlock = _cli.NeoAPI.GetBlock(new UInt256(NeoUtils.ReverseHex(hash.ToString()).HexToBytes()));
                    }
                    else
                    {
                        neoBlock = _cli.NeoAPI.GetBlock(height);
                    }

                    if (neoBlock == null)
                    {
                        throw new OracleException($"Neo block is null");
                    }

                    var coldStorage = Settings.Instance.Oracle.SwapColdStorageNeo;
                    interopTuple = NeoInterop.MakeInteropBlock(neoBlock, _cli.NeoAPI,
                            ((TokenSwapper)tokenSwapper).SwapAddresses[platformName], coldStorage);
                    break;
                case EthereumWallet.EthereumPlatform:
                    var hashes = nexus.GetPlatformTokenHashes(EthereumWallet.EthereumPlatform, nexus.RootStorage)
                        .Select(x => x.ToString().Substring(0, 40)).ToArray();
                
                    interopTuple = EthereumInterop.MakeInteropBlock(nexus, _cli.EthAPI, height,
                            hashes, Settings.Instance.Oracle.EthConfirmations, ((TokenSwapper)tokenSwapper).SwapAddresses[platformName]);
                    break;

                default:
                    throw new OracleException("Unkown oracle platform: " + platformName);
            }

            if (interopTuple.Item1.Hash != Hash.Null)
            {

                var initialStore = Persist<InteropBlock>(platformName, chainName, interopTuple.Item1.Hash, StorageConst.Block,
                        interopTuple.Item1);
                var transactions = interopTuple.Item2;

                if (!initialStore)
                {
                    Log.Debug($"Oracle block { interopTuple.Item1.Hash } on platform { platformName } updated!");
                }

                foreach (var tx in transactions)
                {
                    var txInitialStore = Persist<InteropTransaction>(platformName, chainName, tx.Hash, StorageConst.Transaction, tx);
                    if (!txInitialStore)
                    {
                        Log.Debug($"Oracle block { interopTuple.Item1.Hash } on platform { platformName } updated!");
                    }
                }

            }

            return interopTuple.Item1;
        }

        /// <summary>
        /// Oracle PullTransactionFromPlatform
        /// </summary>
        /// <param name="platformName"></param>
        /// <param name="chainName"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        /// <exception cref="OracleException"></exception>
        protected override InteropTransactionData PullTransactionFromPlatform(string platformName, string chainName, Hash hash)
        {
            Log.Debug($"{platformName} pull tx: {hash}");
            InteropTransactionData tx = Read<InteropTransactionData>(platformName, chainName, hash, StorageConst.Transaction);
            if (tx != null && tx.Hash != Hash.Null)
            {
                Log.Debug($"Found tx {hash} in oracle storage");
                return tx;
            }

            var nexus = NexusAPI.GetNexus();
            TransactionReceipt txRcpt = null;
            var swappers = nexus.GetSwappersForPlatformAndSymbol(platformName, DomainSettings.FuelTokenSymbol, nexus.RootStorage);
            switch (platformName)
            {
                case EthereumWallet.EthereumPlatform:
                    txRcpt = _cli.EthAPI.GetTransactionReceipt(hash.ToString());
                    tx = EthereumInterop.MakeInteropTransaction(txRcpt, _cli.EthAPI, swappers.ToList());
                    break;
                case BSCWallet.BSCPlatform:
                    txRcpt = _cli.BscAPI.GetTransactionReceipt(hash.ToString());
                    tx = EthereumInterop.MakeInteropTransaction(txRcpt, _cli.BscAPI, swappers.ToList());
                    break;
                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }
            
            if (!Persist<InteropTransactionData>(platformName, chainName, tx.Hash, StorageConst.Transaction, tx))
            {
                Log.Error($"Oracle transaction { hash } on platform { platformName } updated!");
            }
            
            return tx;
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            Log.Debug($"{platformName} pull tx: {hash}");
            InteropTransaction tx = Read<InteropTransaction>(platformName, chainName, hash, StorageConst.Transaction);
            if (tx != null && tx.Hash != Hash.Null)
            {
                Log.Debug($"Found tx {hash} in oracle storage");
                return tx;
            }

            var nexus = NexusAPI.GetNexus();
            var tokenSwapper = NexusAPI.GetTokenSwapper();

            switch (platformName)
            {
                case NeoWallet.NeoPlatform:
                    NeoTx neoTx;
                    UInt256 uHash = new UInt256(NeoUtils.ReverseHex(hash.ToString()).HexToBytes());
                    neoTx = _cli.NeoAPI.GetTransaction(uHash);
                    var coldStorage = Settings.Instance.Oracle.SwapColdStorageNeo;
                    tx = NeoInterop.MakeInteropTx(neoTx, _cli.NeoAPI, ((TokenSwapper)tokenSwapper).SwapAddresses[platformName], coldStorage);
                    break;
                case EthereumWallet.EthereumPlatform:
                    var txRcpt = _cli.EthAPI.GetTransactionReceipt(hash.ToString());
                    tx = EthereumInterop.MakeInteropTx(nexus, txRcpt, _cli.EthAPI, ((TokenSwapper)tokenSwapper).SwapAddresses[platformName]);
                    break;
                case BSCWallet.BSCPlatform:
                    var txRcptBSC = _cli.BscAPI.GetTransactionReceipt(hash.ToString());
                    tx = EthereumInterop.MakeInteropTx(nexus, txRcptBSC, _cli.BscAPI, ((TokenSwapper)tokenSwapper).SwapAddresses[platformName]);
                    break;

                default:
                    throw new OracleException("Uknown oracle platform: " + platformName);
            }

            if (!Persist<InteropTransaction>(platformName, chainName, tx.Hash, StorageConst.Transaction, tx))
            {
                Log.Error($"Oracle transaction { hash } on platform { platformName } updated!");
            }

            return tx;
        }

        protected override T PullData<T>(Timestamp time, string url)
        {
            throw new OracleException("unknown oracle url");
        }

        protected override InteropNFT PullPlatformNFT(string platformName, string symbol, BigInteger tokenID)
        {
            // TODO NFT support
            throw new NotImplementedException();
        }
    }

    struct CachedFee
    {
        public Timestamp Time;
        public BigInteger Value;

        public CachedFee(Timestamp time, BigInteger value)
        {
            this.Time = time;
            this.Value = value;
        }
    }
}
