using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Phantasma.Infrastructure.API;
using Phantasma.Infrastructure.API.Controllers;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Node.Chains.Ethereum;
using Phantasma.Node.Chains.Neo2;
using Serilog;
using TransactionResult = Phantasma.Infrastructure.API.TransactionResult;

namespace Phantasma.Node.Interop
{
    public enum SwapStatus
    {
        InProgress,
        Settle,
        Confirm,
        Finished
    }

    public struct PendingSwap: ISerializable
    {
        public string platform;
        public Hash hash;
        public Address source;
        public Address destination;

        public PendingSwap(string platform, Hash hash, Address source, Address destination)
        {
            this.platform = platform;
            this.hash = hash;
            this.source = source;
            this.destination = destination;
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(platform);
            writer.WriteHash(hash);
            writer.WriteAddress(source);
            writer.WriteAddress(destination);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.platform = reader.ReadVarString();
            this.hash = reader.ReadHash();
            this.source = reader.ReadAddress();
            this.destination = reader.ReadAddress();
        }
    }

    public struct PendingFee : ISerializable
    {
        public Hash sourceHash;
        public Hash destinationHash;
        public Hash settleHash;
        public Timestamp time;
        public SwapStatus status;

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteHash(sourceHash);
            writer.WriteHash(destinationHash);
            writer.WriteHash(settleHash);
            writer.Write(time.Value);
            writer.Write((byte)status);
        }

        public void UnserializeData(BinaryReader reader)
        {
            sourceHash = reader.ReadHash();
            destinationHash = reader.ReadHash();
            settleHash = reader.ReadHash();
            time = new Timestamp(reader.ReadUInt32());
            this.status = (SwapStatus)reader.ReadByte();
        }
    }

    public abstract class ChainSwapper
    {
        public readonly string PlatformName;
        public readonly TokenSwapper Swapper;
        public readonly string LocalAddress;
        public readonly string WIF;
        public IOracleReader OracleReader => Swapper.OracleReader;

        protected ChainSwapper(TokenSwapper swapper, string platformName)
        {
            Swapper = swapper;

            this.WIF = Settings.Instance.GetInteropWif(swapper.SwapKeys, platformName);
            this.PlatformName = platformName;
            this.LocalAddress = swapper.FindAddress(platformName);

            // for testing with mainnet swap address
            //this.LocalAddress = "AbFdbvacCeBrncvwYnPEtfKqyr5KU9SWAU"; //swapper.FindAddress(platformName);

            if (string.IsNullOrEmpty(LocalAddress))
            {
                throw new SwapException($"Invalid address for {platformName} swaps");
            }

            var localKeys = GetAvailableAddress(this.WIF);
            if (localKeys == LocalAddress)
            {
                Log.Information($"Listening for {platformName} swaps at address {LocalAddress.ToLower()}");
            }
            else
            {
                Log.Error($"Expected {platformName} keys to {LocalAddress}, instead got keys to {localKeys}");
            }
        }

        protected abstract string GetAvailableAddress(string wif);
        public abstract IEnumerable<PendingSwap> Update();
        public abstract void ResyncBlock(System.Numerics.BigInteger blockId);

        internal abstract Hash SettleSwap(Hash sourceHash, Address destination, IToken token, BigInteger amount);
        internal abstract Hash VerifyExternalTx(Hash sourceHash, string txStr);
    }

    public class TokenSwapper : ITokenSwapper
    {
        public Dictionary<string, string[]> SwapAddresses = new Dictionary<string,string[]>();

        internal readonly PhantasmaKeys SwapKeys;
        internal readonly IOracleReader OracleReader;

        public readonly Node Node;

        internal readonly StorageContext Storage;
        private readonly BigInteger MinimumFee;
        private readonly NeoAPI neoAPI;
        private readonly EthAPI ethAPI;

        private readonly Dictionary<string, BigInteger> interopBlocks;
        private PlatformInfo[] platforms;
        private Dictionary<string, ChainSwapper> _swappers = new Dictionary<string, ChainSwapper>();

        private HashSet<string> _supportedPlatforms = new HashSet<string>();

        public TokenSwapper(Node node, PhantasmaKeys swapKey, NeoAPI neoAPI, EthAPI ethAPI, BigInteger minFee, string[] supportedPlatforms)
        {
            var nexus = NexusAPI.GetNexus();

            this.Node = node;
            this.SwapKeys = swapKey;
            this.OracleReader = nexus.GetOracleReader();
            this.MinimumFee = minFee;
            this.neoAPI = neoAPI;
            this.ethAPI = ethAPI;

            this.Storage = new KeyStoreStorage(nexus.CreateKeyStoreAdapter("swaps"));

            this.interopBlocks = new Dictionary<string, BigInteger>();

            interopBlocks[DomainSettings.PlatformName] = BigInteger.Parse(Settings.Instance.Oracle.PhantasmaInteropHeight);
            interopBlocks["neo"] = BigInteger.Parse(Settings.Instance.Oracle.NeoInteropHeight);
            interopBlocks["ethereum"] = BigInteger.Parse(Settings.Instance.Oracle.EthInteropHeight);

            var inProgressMap = new StorageMap(InProgressTag, this.Storage);

            Console.WriteLine($"inProgress count: {inProgressMap.Count()}");
            inProgressMap.Visit<Hash, string>((key, value) =>
            {
                if (!string.IsNullOrEmpty(value))
                    Console.WriteLine($"inProgress: {key} - {value}");
            });



            _supportedPlatforms.Add(DomainSettings.PlatformName);
            foreach (var entry in supportedPlatforms)
            {
                if (_supportedPlatforms.Contains(entry))
                {
                    throw new SwapException($"Duplicated swap platform {entry}, check config");
                }

                if (!interopBlocks.ContainsKey(entry))
                {
                    throw new SwapException($"Unknown swap platform {entry}, check config");
                }

                _supportedPlatforms.Add(entry);
            }
        }

        internal IToken FindTokenByHash(string asset, string platform)
        {
            var nexus = NexusAPI.GetNexus();

            var hash = Hash.FromUnpaddedHex(asset);
            var symbols = nexus.GetTokens(nexus.RootStorage);

            foreach (var symbol in symbols)
            {
                var otherHash = nexus.GetTokenPlatformHash(symbol, platform, nexus.RootStorage);
                if (hash == otherHash)
                {
                    return nexus.GetTokenInfo(nexus.RootStorage, symbol);
                }
            }

            return null;
        }

        internal string FindAddress(string platformName)
        {
            //TODO use last address for now, needs to be fixed in the future
            return platforms.Where(x => x.Name == platformName).Select(x => x.InteropAddresses[x.InteropAddresses.Length-1].ExternalAddress).FirstOrDefault();
        }

        internal const string SettlementTag = ".settled";
        internal const string PendingTag = ".pending";
        internal const string InProgressTag = ".inprogress";
        internal const string UsedRpcTag = ".usedrpc";

        // This lock added to protect from reading wrong state (settled, pending, inprogress)
        // while it's being modified by concurrent thread.
        private static object StateModificationLock = new object();

        private Dictionary<Hash, PendingSwap> _pendingSwaps = new Dictionary<Hash, PendingSwap>();
        private Dictionary<Address, List<Hash>> _swapAddressMap = new Dictionary<Address, List<Hash>>();
        private Dictionary<string, Task<IEnumerable<PendingSwap>>> taskDict = new Dictionary<string, Task<IEnumerable<PendingSwap>>>();

        private void MapSwap(Address address, Hash hash)
        {
            List<Hash> list;

            if (_swapAddressMap.ContainsKey(address))
            {
                list = _swapAddressMap[address];
            }
            else
            {
                list = new List<Hash>();
                _swapAddressMap[address] = list;
            }

            list.Add(hash);
        }

        public void ResyncBlockOnChain(string platform, string blockId)
        {
            if (_swappers.TryGetValue(platform, out ChainSwapper finder)
                    && System.Numerics.BigInteger.TryParse(blockId, out var bigIntBlock))
            {
                Log.Information($"TokenSwapper: Resync block {blockId} on platform {platform}");
                finder.ResyncBlock(bigIntBlock);
            }
            else
            {
                Log.Error($"TokenSwapper: Resync block {blockId} on platform {platform} failed.");
            }
        }

        public void Update()
        {
            try
            {
                var nexus = NexusAPI.GetNexus();

                if (this.platforms == null)
                {
                    if (!nexus.HasGenesis())
                    {
                        return;
                    }

                    var platforms = nexus.GetPlatforms(nexus.RootStorage);
                    this.platforms = platforms.Select(x => nexus.GetPlatformInfo(nexus.RootStorage, x)).ToArray();

                    if (this.platforms.Length == 0)
                    {
                        Log.Warning("No interop platforms found. Make sure that the Nexus was created correctly.");
                        return;
                    }

                    _swappers["neo"] = new NeoInterop(this, neoAPI, interopBlocks["neo"], Settings.Instance.Oracle.NeoQuickSync);
                    var platformInfo = nexus.GetPlatformInfo(nexus.RootStorage, "neo");
                    SwapAddresses["neo"] = platformInfo.InteropAddresses.Select(x => x.ExternalAddress).ToArray();

                    _swappers["ethereum"] = new EthereumInterop(this, ethAPI, interopBlocks["ethereum"], nexus.GetPlatformTokenHashes("ethereum", nexus.RootStorage).Select(x => x.ToString().Substring(0, 40)).ToArray(), Settings.Instance.Oracle.EthConfirmations);
                    platformInfo = nexus.GetPlatformInfo(nexus.RootStorage, "ethereum");
                    SwapAddresses["ethereum"] = platformInfo.InteropAddresses.Select(x => x.ExternalAddress).ToArray();

                    Log.Information("Available swap addresses:");
                    foreach (var x in SwapAddresses)
                    {
                        Log.Information("platform: " + x.Key + " address: " + string.Join(", ", x.Value));
                    }
                }

                if (this.platforms.Length == 0)
                {
                    return;
                }
                else
                {
                    if (taskDict.Count == 0)
                    {
                        foreach (var platform in this.platforms)
                        {
                            taskDict.Add(platform.Name, null);
                        }
                    }
                }

                lock (StateModificationLock)
                {
                    var pendingList = new StorageList(PendingTag, this.Storage);

                    int i = 0;
                    var count = pendingList.Count();

                    while (i < count)
                    {
                        var settlement = pendingList.Get<PendingFee>(i);
                        if (UpdatePendingSettle(pendingList, i))
                        {
                            pendingList.RemoveAt(i);
                            count--;
                        }
                        else
                        {
                            i++;
                        }
                    }
                }

                ProcessCompletedTasks();

                for (var j = 0; j < taskDict.Count; j++)
                {
                    var platform = taskDict.Keys.ElementAt(j);
                    var task = taskDict[platform];
                    if (task == null)
                    {
                        if (_swappers.TryGetValue(platform, out ChainSwapper finder))
                        {
                            taskDict[platform] = new Task<IEnumerable<PendingSwap>>(() =>
                                                    {
                                                        return finder.Update();
                                                    });
                        }
                    }
                }

                // start new tasks
                foreach (var entry in taskDict)
                {
                    var task = entry.Value;
                    if (task != null && task.Status.Equals(TaskStatus.Created))
                    {
                        task.ContinueWith(t => { Console.WriteLine($"===> task {task.ToString()} failed"); }, TaskContinuationOptions.OnlyOnFaulted);
                        task.Start();
                    }
                }
            }
            catch (Exception e)
            {
                var logMessage = "TokenSwapper.Update() exception caught:\n" + e.Message;
                var inner = e.InnerException;
                while (inner != null)
                {
                    logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;
                    inner = inner.InnerException;
                }
                logMessage += "\n\n" + e.StackTrace;

                Log.Error(logMessage);
            }
        }

        private void ProcessCompletedTasks()
        {
            for (var i = 0; i < taskDict.Count; i++)
            {
                var platform = taskDict.Keys.ElementAt(i);
                var task = taskDict[platform];
                if (task != null)
                {
                    if (task.IsCompleted)
                    {
                        if (task.IsFaulted)
                        {
                            taskDict[platform] = null;
                            continue;
                        }
                        else
                        {
                            var swaps = task.Result;
                            foreach (var swap in swaps)
                            {
                                if (_pendingSwaps.ContainsKey(swap.hash))
                                {

                                    Log.Information($"Already known swap, ignore {swap.platform} swap: {swap.source} => {swap.destination}");
                                    continue;
                                }

                                Log.Information($"Detected {swap.platform} swap: {swap.source} => {swap.destination} hash: {swap.hash}");
                                _pendingSwaps[swap.hash] = swap;
                                MapSwap(swap.source, swap.hash);
                                MapSwap(swap.destination, swap.hash);
                            }
                            taskDict[platform] = null;
                        }
                    }
                }
            }
        }

        public Hash SettleSwap(string sourcePlatform, string destPlatform, Hash sourceHash)
        {
            Log.Debug("settleSwap called " + sourceHash);
            Log.Debug("dest platform " + destPlatform);
            Log.Debug("src platform " + sourcePlatform);

            // This code is preventing us from doing double swaps.
            // We must ensure that states (settled, pending, inprogress) are locked
            // during this check and can't be changed by concurrent thread,
            // making this check inconsistent.
            lock (StateModificationLock)
            {
                // First thing, check if the sourceHash is already known, if so, return
                var inProgressMap = new StorageMap(InProgressTag, this.Storage);
                if (inProgressMap.ContainsKey(sourceHash))
                {
                    Log.Debug("Hash already known, swap currently in progress: " + sourceHash);

                    var tx = inProgressMap.Get<Hash, string>(sourceHash);

                    if (string.IsNullOrEmpty(tx))
                    {
                        // no tx was created, so no reason to keep the entry, we can't verify anything anyway.
                        Log.Debug("No tx hash set, swap in progress: " + sourceHash);
                        return Hash.Null;
                    }
                    else
                    {
                        var chainSwapper = _swappers[destPlatform];
                        var destHash = chainSwapper.VerifyExternalTx(sourceHash, tx);

                        return destHash;
                    }
                }
                else
                {
                    var settleHash = GetSettleHash(sourcePlatform, sourceHash);
                    Log.Debug("settleHash in settleswap: " + settleHash);

                    if (settleHash != Hash.Null)
                    {
                        return settleHash;
                    }
                    else
                    {
                        // sourceHash not known, create an entry to store it, from here on,
                        // every call to SettleSwap will return Hash.Null until the swap is finished.
                        Log.Debug("Unknown hash, create in progress entry: " + sourceHash);
                        inProgressMap.Set<Hash, string>(sourceHash, null);
                    }
                }
            }

            if (destPlatform == PhantasmaWallet.PhantasmaPlatform)
            {
                return SettleTransaction(sourcePlatform, sourcePlatform, sourceHash);
            }

            if (sourcePlatform != PhantasmaWallet.PhantasmaPlatform)
            {
                throw new SwapException("Invalid source platform");
            }

            return SettleSwapToExternal(sourceHash, destPlatform);
        }


        // should only be called from inside lock block
        private Hash GetSettleHash(string sourcePlatform, Hash sourceHash)
        {
            var settlements = new StorageMap(SettlementTag, this.Storage);

            if (settlements.ContainsKey<Hash>(sourceHash))
            {
                return settlements.Get<Hash, Hash>(sourceHash);
            }

            var pendingList = new StorageList(PendingTag, this.Storage);
            var count = pendingList.Count();
            for (int i = 0; i < count; i++)
            {
                var settlement = pendingList.Get<PendingFee>(i);
                if (settlement.sourceHash == sourceHash)
                {
                    return settlement.destinationHash;
                }
            }

            var nexus = NexusAPI.GetNexus();

            var hash = (Hash)nexus.RootChain.InvokeContractAtTimestamp(nexus.RootStorage, Timestamp.Now, "interop", nameof(InteropContract.GetSettlement), sourcePlatform, sourceHash).ToObject();
            if (hash != Hash.Null && !settlements.ContainsKey<Hash>(sourceHash))
            {
                // This modification should be locked when GetSettleHash() is called from SettleSwap(),
                // so we lock it in SettleSwap().
                settlements.Set<Hash, Hash>(sourceHash, hash);
            }
            return hash;
        }

        private Hash SettleTransaction(string sourcePlatform, string chain, Hash txHash)
        {
            var script = new ScriptBuilder().
                AllowGas(SwapKeys.Address, Address.Null, MinimumFee, Transaction.DefaultGasLimit).
                CallContract("interop", nameof(InteropContract.SettleTransaction), SwapKeys.Address, sourcePlatform, chain, txHash).
                SpendGas(SwapKeys.Address).
                EndScript();

            var nexus = NexusAPI.GetNexus();

            var tx = new Transaction(nexus.Name, "main", script, Timestamp.Now + TimeSpan.FromMinutes(5), Node.TxIdentifier);
            tx.Sign(SwapKeys);

            var bytes = tx.ToByteArray(true);

            var txData = Base16.Encode(bytes);
            try
            {
                var transactionController = new TransactionController();
                transactionController.SendRawTransaction(txData);
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Exception caught during transaction settling");
                return Hash.Null;
            }

            return tx.Hash;
        }

        public IEnumerable<ChainSwap> GetPendingSwaps(Address address)
        {
            Log.Debug($"Getting pending swaps for {address} now.");
            var dict = new Dictionary<Hash, ChainSwap>();

            if (_swapAddressMap.ContainsKey(address))
            {
                Log.Debug($"Address exists in swap address map");

                var swaps = _swapAddressMap[address].
                    Select(x => _pendingSwaps[x]).
                    Select(x => new ChainSwap(x.platform, x.platform, x.hash, DomainSettings.PlatformName, DomainSettings.RootChainName, Hash.Null));

                foreach (var entry in swaps)
                {
                    Log.Debug($"Adding hash {entry.sourceHash} to dict.");
                    dict[entry.sourceHash] = entry;
                }

                var keys = dict.Keys.ToArray();
                foreach (var hash in keys)
                {
                    var entry = dict[hash];
                    if (entry.destinationHash == Hash.Null)
                    {
                        lock (StateModificationLock)
                        {
                            var settleHash = GetSettleHash(entry.sourcePlatform, hash);
                            Log.Debug($"settleHash: {settleHash}.");
                            if (settleHash != Hash.Null)
                            {
                                entry.destinationHash = settleHash;
                                dict[hash] = entry;
                            }
                        }
                    }
                }

            }

            var nexus = NexusAPI.GetNexus();

            var hashes = nexus.RootChain.GetSwapHashesForAddress(nexus.RootChain.Storage, address);
            Log.Debug($"Have {hashes.Length} for address {address}.");
            foreach (var hash in hashes)
            {
                if (dict.ContainsKey(hash))
                {
                    Log.Debug($"Ignoring hash {hash}");
                    continue;
                }

                var swap = nexus.RootChain.GetSwap(nexus.RootChain.Storage, hash);
                if (swap.destinationHash != Hash.Null)
                {
                    Log.Debug($"Ignoring swap with hash {swap.sourceHash}");
                    continue;
                }

                lock (StateModificationLock)
                {
                    var settleHash = GetSettleHash(DomainSettings.PlatformName, hash);
                    if (settleHash != Hash.Null)
                    {
                        Log.Debug($"settleHash null");
                        continue;
                    }
                }

                dict[hash] = swap;
            }

            Log.Debug($"Getting pending swaps for {address} done, found {dict.Count()} swaps.");
            return dict.Values;
        }


        private Hash SettleSwapToExternal(Hash sourceHash, string destPlatform)
        {
            var swap = OracleReader.ReadTransaction(DomainSettings.PlatformName, DomainSettings.RootChainName, sourceHash);
            var transfers = swap.Transfers.Where(x => x.destinationAddress.IsInterop).ToArray();

            // TODO not support yet
            if (transfers.Length != 1)
            {
                Log.Warning($"Not implemented: Swap support for multiple transfers in a single transaction");
                return Hash.Null;
            }

            var transfer = transfers[0];

            var nexus = NexusAPI.GetNexus();

            var token = nexus.GetTokenInfo(nexus.RootStorage, transfer.Symbol);

            lock (StateModificationLock)
            {
                var destHash = GetSettleHash(DomainSettings.PlatformName, sourceHash);
                Log.Debug("settleHash in settleswap: " + destHash);

                if (destHash != Hash.Null)
                {
                    return destHash;
                }

                if (!_swappers.ContainsKey(destPlatform))
                {
                    return Hash.Null; // just in case, should never happen
                }

                var chainSwapper = _swappers[destPlatform];

                destHash = chainSwapper.SettleSwap(sourceHash, transfer.destinationAddress, token, transfer.Value);

                // if the asset transfer was sucessfull, we prepare a fee settlement on the mainnet
                if (destHash != Hash.Null)
                {
                    var pendingList = new StorageList(PendingTag, this.Storage);
                    var settle = new PendingFee() { sourceHash = sourceHash, destinationHash = destHash, settleHash = Hash.Null, time = DateTime.UtcNow, status = SwapStatus.Settle };
                    pendingList.Add<PendingFee>(settle);
                }

                return destHash;
            }
        }

        // NOTE no locks here because we call this from within a lock already
        private bool UpdatePendingSettle(StorageList list, int index)
        {
            var swap = list.Get<PendingFee>(index);
            var prevStatus = swap.status;
            switch (swap.status)
            {
                case SwapStatus.Settle:
                    {
                        var diff = Timestamp.Now - swap.time;
                        if (diff >= 60)
                        {
                            swap.settleHash = SettleTransaction(DomainSettings.PlatformName, DomainSettings.RootChainName, swap.sourceHash);
                            if (swap.settleHash != Hash.Null)
                            {
                                swap.status = SwapStatus.Confirm;
                            }
                        }
                        break;
                    }

                case SwapStatus.Confirm:
                    {
                        try
                        {
                            var transactionController = new TransactionController();
                            var result = transactionController.GetTransaction(swap.settleHash.ToString());

                            var tx = (TransactionResult)result;
                            swap.status = SwapStatus.Finished;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("pending")) // TODO improve
                            {
                                swap.settleHash = Hash.Null;
                                swap.time = Timestamp.Now;
                                swap.status = SwapStatus.Settle;
                            }
                        }
                        break;
                    }

                default: return false;
            }

            if (swap.status == SwapStatus.Finished)
            {
                var settlements = new StorageMap(SettlementTag, this.Storage);
                settlements.Set<Hash, Hash>(swap.sourceHash, swap.destinationHash);

                // swap is finished, it's safe to remove it from inProgressMap
                var inProgressMap = new StorageMap(InProgressTag, this.Storage);
                inProgressMap.Remove<Hash>(swap.sourceHash);
                return true;
            }

            if (swap.status != prevStatus)
            {
                list.Replace<PendingFee>(index, swap);
            }

            return false;
        }

        public bool SupportsSwap(string sourcePlatform, string destPlatform)
        {
            return (sourcePlatform != destPlatform) && _supportedPlatforms.Contains(sourcePlatform) && _supportedPlatforms.Contains(destPlatform) && (sourcePlatform == DomainSettings.PlatformName || destPlatform == DomainSettings.PlatformName);
        }
    }
}
