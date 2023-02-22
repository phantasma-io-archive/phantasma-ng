﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Business.Blockchain
{
    public abstract class OracleReader : IOracleReader
    {
        public const string interopTag = "interop://";
        public const string priceTag = "price://";
        public const string feeTag = "fee://";
        public BigInteger ProtocolVersion => Nexus.GetGovernanceValue(Nexus.RootChain.StorageCollection.ContractsStorage, Nexus.NexusProtocolVersionTag);

        protected ConcurrentDictionary<string, OracleEntry> _entries = new ConcurrentDictionary<string, OracleEntry>();
        protected ConcurrentDictionary<string, OracleEntry> _txEntries = new ConcurrentDictionary<string, OracleEntry>();

        public IEnumerable<OracleEntry> Entries => (IEnumerable<OracleEntry>) _entries.Values;

        protected abstract T PullData<T>(Timestamp time, string url);
        protected abstract decimal PullPrice(Timestamp time, string symbol);
        protected abstract BigInteger PullFee(Timestamp time, string platform);
        protected abstract InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, BigInteger height = new BigInteger());
        protected abstract InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash);
        protected abstract InteropNFT PullPlatformNFT(string platformName, string symbol, BigInteger tokenID);
        public abstract string GetCurrentHeight(string platformName, string chainName);
        public abstract void SetCurrentHeight(string platformName, string chainName, string height);
        public abstract List<InteropBlock> ReadAllBlocks(string platformName, string chainName);

        public readonly Nexus Nexus;

        public OracleReader(Nexus nexus)
        {
            this.Nexus = nexus;
        }

        public virtual T Read<T>(Timestamp time, string url) where T : class
        {
            if (TryGetOracleCache<T>(url, out T cachedEntry))
            {
                return cachedEntry as T;
            }

            T content;

            if (url.StartsWith(interopTag))
            {
                var tags = url.Substring(interopTag.Length);
                var args = tags.Split('/');

                var platformName = args[0];
                var chainName = args[1];

                if (chainName == "nft")
                {
                    args = args.Skip(2).ToArray();
                    content = (T)(object)ReadNFTOracle(platformName, args);
                }
                else if (Nexus.PlatformExists(Nexus.RootChain.StorageCollection.PlatformsStorage, platformName))
                {
                    args = args.Skip(2).ToArray();
                    content = ReadChainOracle<T>(platformName, chainName, args);

                    if (content is InteropBlock)
                    {
                        if ((content as InteropBlock).Hash == Hash.Null)
                        {
                            return content;
                        }
                    }

                    if (content is InteropTransaction)
                    {
                        if ((content as InteropTransaction).Hash == Hash.Null)
                        {
                            return content;
                        }
                    }
                }
                else
                {
                    throw new OracleException("invalid oracle platform: " + platformName);
                }
            }
            else if (url.StartsWith(priceTag))
            {
                var baseSymbol = url.Substring(priceTag.Length);

                if (baseSymbol.Contains('/'))
                {
                    throw new OracleException("invalid oracle price request");
                }

                BigInteger val;

                if (!Nexus.TokenExists(Nexus.RootChain.StorageCollection.ContractsStorage, baseSymbol))
                {
                    throw new OracleException("unknown token: " + baseSymbol);
                }

                if (baseSymbol == DomainSettings.FuelTokenSymbol)
                {
                    var soulPriceBi = this.ReadPrice(time, DomainSettings.StakingTokenSymbol);
                    var soulPriceDec = UnitConversion.ToDecimal(soulPriceBi, DomainSettings.FiatTokenDecimals);
                    val = UnitConversion.ToBigInteger(soulPriceDec/5, DomainSettings.FiatTokenDecimals);
                }
                else
                {
                    var price = PullPrice(time, baseSymbol);
                    val = UnitConversion.ToBigInteger(price, DomainSettings.FiatTokenDecimals);
                }

                content = val.ToSignedByteArray() as T;
            }
            else if (url.StartsWith(feeTag))
            {
                var platform = url.Substring(feeTag.Length);

                if (platform.Contains('/'))
                {
                    throw new OracleException("invalid oracle fee request");
                }

                if (!Nexus.PlatformExists(Nexus.RootChain.StorageCollection.PlatformsStorage, platform))
                {
                    throw new OracleException("unknown platform: " + platform);
                }

                var val = PullFee(time, platform);
                content = val.ToSignedByteArray() as T;
            }
            else
            {
                content = PullData<T>(time, url);
            }

            CacheOracleData<T>(url, content);

            return content;
        }

        private bool TryGetOracleCache<T>(string url, out T content)
        {
            lock (_txEntries)
            {
                if (_txEntries.ContainsKey(url))
                {
                    content = Serialization.Unserialize<T>(_txEntries[url].Content);
                    return true;
                }
            }

            lock (_entries)
            {
                if (_entries.ContainsKey(url))
                {
                    content = Serialization.Unserialize<T>(_entries[url].Content);
                    return true;
                }
            }

            content = default(T);
            return false;
        }

        private void CacheOracleData<T>(string url, T content)
        {
            if (content == null)
            {
                return;
            }

            var value = Serialization.Serialize(content);
            if (value == null)
            {
                throw new OracleException($"Serialized value can't be null, url: {url}");
            }

            var entry = new OracleEntry(url, value);
            lock (_txEntries)
            {
                _txEntries[url] = entry;
            }
        }

        private bool FindMatchingEvent(IEnumerable<Event> events, out Event output, Func<Event, bool> predicate)
        {
            foreach (var evt in events)
            {
                if (predicate(evt))
                {
                    output = evt;
                    return true;
                }
            }

            output = new Event();
            return false;
        }

        private T ReadChainOracle<T>(string platformName, string chainName, string[] input) where T : class
        {
            if (input == null || input.Length != 2)
            {
                throw new OracleException("missing oracle input");
            }

            var cmd = input[0].ToLower();
            switch (cmd)
            {
                case "tx":
                case "transaction":
                    {
                        Hash hash;
                        if (Hash.TryParse(input[1], out hash))
                        {
                            InteropTransaction tx;

                            if (platformName == DomainSettings.PlatformName)
                            {
                                var chain = Nexus.GetChainByName(chainName);

                                var blockHash = chain.GetBlockHashOfTransaction(hash);
                                var block = chain.GetBlockByHash(blockHash);

                                var temp = chain.GetTransactionByHash(hash);
                                if (block == null || temp == null)
                                {
                                    throw new OracleException($"invalid transaction hash for chain {chainName} @ {platformName}");
                                }

                                var events = block.GetEventsForTransaction(hash);
                                var transfers = new List<InteropTransfer>();
                                foreach (var evt in events)
                                {
                                    switch (evt.Kind)
                                    {
                                        case EventKind.TokenSend:
                                            {
                                                var data = evt.GetContent<TokenEventData>();
                                                Event other;
                                                if (FindMatchingEvent(events, out other,
                                                    (x) =>
                                                    {
                                                        if (x.Kind != EventKind.TokenReceive && x.Kind != EventKind.TokenStake)
                                                        {
                                                            return false;
                                                        }

                                                        var y = x.GetContent<TokenEventData>();
                                                        return y.Symbol == data.Symbol && y.Value == data.Value;
                                                    }))
                                                {
                                                    var otherData = other.GetContent<TokenEventData>();

                                                    byte[] rawData = null;

                                                    var token = Nexus.GetTokenInfo(Nexus.StorageCollection.ContractsStorage, data.Symbol);
                                                    if (!token.IsFungible())
                                                    {
                                                        Event nftEvent;
                                                        if (!FindMatchingEvent(events, out nftEvent,
                                                            (x) =>
                                                            {
                                                                if (x.Kind != EventKind.PackedNFT)
                                                                {
                                                                    return false;
                                                                }

                                                                var y = x.GetContent<PackedNFTData>();
                                                                return y.Symbol == data.Symbol;
                                                            }))
                                                        {
                                                            throw new OracleException($"invalid nft transfer with hash in chain {chainName} @ {platformName}");
                                                        }

                                                        rawData = nftEvent.Data;
                                                    }

                                                    transfers.Add(new InteropTransfer(data.ChainName, evt.Address, otherData.ChainName, other.Address, Address.Null, data.Symbol, data.Value, rawData));
                                                }
                                                break;
                                            }
                                    }
                                }

                                tx = new InteropTransaction(hash, transfers);
                                if (typeof(T) == typeof(byte[]))
                                {
                                    return Serialization.Serialize(tx) as T;
                                }
                            }
                            else
                            {
                                tx = PullPlatformTransaction(platformName, chainName, hash);

                                if (tx == null)
                                {
                                    return null;
                                }
                            }

                            if (typeof(T) == typeof(byte[]))
                            {
                                return Serialization.Serialize(tx) as T;
                            }

                            return tx as T;
                        }
                        else
                        {
                            throw new OracleException($"invalid transaction hash for chain {chainName} @ {platformName}");
                        }
                    }

                case "block":
                    {
                        Hash hash;
                        InteropBlock block;
                        BigInteger height;
                        // if it fails it might be block height
                        if (Hash.TryParse(input[1], out hash))
                        {
                            if (platformName == DomainSettings.PlatformName)
                            {
                                var chain = Nexus.GetChainByName(chainName);
                                var temp = chain.GetBlockByHash(hash);
                                if (temp == null)
                                {
                                    throw new OracleException($"invalid block hash for chain {chainName} @ {platformName}");
                                }

                                block = new InteropBlock(platformName, chainName, hash, temp.TransactionHashes);
                            }
                            else
                            {
                                block = PullPlatformBlock(platformName, chainName, hash);
                            }

                            if (typeof(T) == typeof(byte[]))
                            {
                                return Serialization.Serialize(block) as T;
                            }

                            return (block) as T;
                        }
                        else if (BigInteger.TryParse(input[1], out height))
                        {
                            if (platformName == DomainSettings.PlatformName)
                            {
                                var chain = Nexus.GetChainByName(chainName);
                                var temp = chain.GetBlockByHash(hash);
                                if (temp == null)
                                {
                                    throw new OracleException($"invalid block hash for chain {chainName} @ {platformName}");
                                }

                                block = new InteropBlock(platformName, chainName, hash, temp.TransactionHashes);
                            }
                            else
                            {
                                block = PullPlatformBlock(platformName, chainName, Hash.Null, height);
                            }

                            if (typeof(T) == typeof(byte[]))
                            {
                                return Serialization.Serialize(block) as T;
                            }

                            return (block) as T;

                        }
                        else
                        {
                            throw new OracleException($"invalid block hash for chain {chainName} @ {platformName}");
                        }
                    }

                default:
                    throw new OracleException("unknown platform oracle");
            }
        }

        private InteropNFT ReadNFTOracle(string platformName, string[] input)
        {
            if (input == null || input.Length != 2)
            {
                throw new OracleException("missing oracle input");
            }

            var symbol = input[0];
            var tokenID = BigInteger.Parse(input[1]);

            if (platformName == DomainSettings.PlatformName)
            {
                var nft = Nexus.ReadNFT(Nexus.RootChain.StorageCollection.ContractsStorage, symbol, tokenID);
                var tokenInfo = Nexus.GetTokenInfo(Nexus.RootChain.StorageCollection.ContractsStorage, symbol);

                var name = $"{tokenInfo.Name} #{tokenID}";

                // TODO proper fetch name + description + url
                return new InteropNFT(name, "No description", "http://TODO");
            }

            return PullPlatformNFT(platformName, symbol, tokenID);
        }

        public InteropTransaction ReadTransaction(string platform, string chain, Hash hash)
        {
            var url = DomainExtensions.GetOracleTransactionURL(platform, chain, hash);
            var bytes = this.Read<InteropTransaction>(Timestamp.Now, url);
            return bytes;
        }

        public void Clear()
        {
            _entries.Clear();
            _txEntries.Clear();
        }

        public void MergeTxData()
        {
            if (_txEntries.Count > 0)
            {
                _entries.Merge(_txEntries);
                _txEntries.Clear();
            }
        }

        // returns how many pending oracle Calls are there for current transaction
        public int GetMultiplier()
        {
            return 1 + _txEntries.Count();
        }
    }
}
