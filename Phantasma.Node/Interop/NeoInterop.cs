﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Neo;
using Neo.Network.P2P.Payloads;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Infrastructure.Pay;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Node.Chains.Neo2;
using Phantasma.Node.Utils;
using Serilog;
using NeoBlock = Neo.Network.P2P.Payloads.Block;
using NeoTx = Neo.Network.P2P.Payloads.Transaction;

namespace Phantasma.Node.Interop
{
    public class NeoInterop : ChainSwapper
    {
        private NeoAPI neoAPI;
        private DateTime lastScan;
        private static bool initialStart = true;
        private bool quickSync = false;

        private List<BigInteger> _resyncBlockIds = new List<BigInteger>();
        private StringLocker _locker;

        public static Dictionary<string, CryptoCurrencyInfo> NeoTokenInfo = new Dictionary<string, CryptoCurrencyInfo>()
        {
            // symbol name dec plat caps
            { "NEO", new CryptoCurrencyInfo("NEO", "NEO", 0, NeoWallet.NeoPlatform, CryptoCurrencyCaps.Balance) },
            { "GAS", new CryptoCurrencyInfo("GAS", "GAS", 8, NeoWallet.NeoPlatform, CryptoCurrencyCaps.Balance) },
            { "SOUL", new CryptoCurrencyInfo("SOUL", "Phantasma Stake", 8, NeoWallet.NeoPlatform, CryptoCurrencyCaps.Balance) },
        };

        public NeoInterop(TokenSwapper swapper, NeoAPI neoAPI, BigInteger interopBlockHeight, bool quickSync)
                : base(swapper, NeoWallet.NeoPlatform)
        {
            string lastBlockHeight = OracleReader.GetCurrentHeight("neo", "neo");
            if (string.IsNullOrEmpty(lastBlockHeight))
                OracleReader.SetCurrentHeight("neo", "neo", interopBlockHeight.ToString());

            this.quickSync = quickSync;

            Log.Information($"interopHeight: {OracleReader.GetCurrentHeight("neo", "neo")}");
            this.neoAPI = neoAPI;

            this.lastScan = DateTime.UtcNow.AddYears(-1);;
            this._locker = new StringLocker();
        }

        protected override string GetAvailableAddress(string wif)
        {
            var neoKeys = NeoKeys.FromWIF(wif);
            return neoKeys.Address;
        }

        public override void ResyncBlock(BigInteger blockId)
        {
            lock(_resyncBlockIds)
            {
                _resyncBlockIds.Add(blockId);
            }
        }

        public override IEnumerable<PendingSwap> Update()
        {
            lock (this._locker.GetLockObject("PendingSetCurrentHeight_" + "neo"))
            {
                var result = new List<PendingSwap>();

                var _interopBlockHeight = BigInteger.Parse(OracleReader.GetCurrentHeight("neo", "neo"));

                // initial start, we have to verify all processed swaps
                if (initialStart)
                {
                    Log.Debug($"Read all neo blocks now.");
                    // TODO check if quick sync nodes are configured, if so use quick sync
                    // we need to find a better solution for that though
                    var allInteropBlocks = OracleReader.ReadAllBlocks("neo", "neo");

                    Log.Debug($"Found {allInteropBlocks.Count} blocks");

                    foreach (var block in allInteropBlocks)
                    {
                        ProcessBlock(block, result);
                    }

                    initialStart = false;

                    Log.Debug($"QuickSync: " + quickSync);
                    // quick sync is only done once after startup
                    if (quickSync)
                    {
                        // if quick sync is active, we can use a specific plugin installed on the nodes (EventTracker)
                        try
                        {
                            var blockIds = neoAPI.GetSwapBlocks("ed07cffad18f1308db51920d99a2af60ac66a7b3", LocalAddress, _interopBlockHeight.ToString());
                            Log.Debug($"Found {blockIds.Count} blocks to process ");
                            List<InteropBlock> blockList = new List<InteropBlock>();
                            foreach (var entry in blockIds)
                            {
                                //Log.Debug($"read block {entry.Value}");
                                var url = DomainExtensions.GetOracleBlockURL("neo", "neo", BigInteger.Parse(entry.Value.ToString()));
                                blockList.Add(OracleReader.Read<InteropBlock>(DateTime.Now, url));
                            }

                            // get blocks and order them for processing
                            var blocksToProcess = blockList.Where(x => blockIds.ContainsKey(x.Hash.ToString()))
                                    .Select(x => new { block = x, id = blockIds[x.Hash.ToString()] })
                                    .OrderBy(x => x.id);

                            Log.Debug($"blocks to process: {blocksToProcess.Count()}");

                            foreach (var entry in blocksToProcess.OrderBy(x => x.id))
                            {
                                Log.Debug($"process block {entry.id}");
                                ProcessBlock(entry.block, result);
                                OracleReader.SetCurrentHeight("neo", "neo", _interopBlockHeight.ToString());
                                _interopBlockHeight = BigInteger.Parse(entry.id.ToString());
                            }

                        }
                        catch (Exception e)
                        {
                            Log.Error("Inital start failed: " + e.ToString());
                        }

                    }

                    // return after the initial start to be able to process all swaps that happend in the mean time.
                    return result;
                }

                try
                {
                    var blockIterator = new NeoBlockIterator(neoAPI);
                    var blockDifference = blockIterator.currentBlock - _interopBlockHeight;
                    var batchCount = (blockDifference > 8) ? 8 : blockDifference; //TODO make it a constant, should be no more than 8

                    while (blockIterator.currentBlock > _interopBlockHeight)
                    {
                        if (_resyncBlockIds.Any())
                        {
                            for (var i = 0; i < _resyncBlockIds.Count; i++)
                            {
                                var blockId = _resyncBlockIds.ElementAt(i);
                                if (blockId > _interopBlockHeight)
                                {
                                    Log.Debug($"NeoInterop: Update() resync block {blockId} higher than current interop height, can't resync.");
                                    _resyncBlockIds.RemoveAt(i);
                                    continue;
                                }

                                Log.Debug($"NeoInterop: Update() resync block {blockId} now.");
                                var interopBlock = GetInteropBlock(blockId);
                                ProcessBlock(interopBlock, result);
                                _resyncBlockIds.RemoveAt(i);
                            }
                        }

                        Log.Debug($"Swaps: Current Neo chain height: {blockIterator.currentBlock}, interop: {_interopBlockHeight}, delta: {blockIterator.currentBlock - _interopBlockHeight}");
                        blockDifference = blockIterator.currentBlock - _interopBlockHeight;
                        batchCount = (blockDifference > 8) ? 8 : blockDifference;

                        if (batchCount > 1)
                        {
                            List<Task<InteropBlock>> taskList = CreateTaskList(batchCount);

                            foreach (var task in taskList)
                            {
                                task.Start();
                            }

                            Task.WaitAll(taskList.ToArray());

                            foreach (var task in taskList)
                            {
                                var block = task.Result;

                                ProcessBlock(block, result);
                            }

                            OracleReader.SetCurrentHeight("neo", "neo", _interopBlockHeight.ToString());
                            _interopBlockHeight += batchCount;
                        }
                        else
                        {
                            var interopBlock = GetInteropBlock(_interopBlockHeight);

                            ProcessBlock(interopBlock, result);

                            OracleReader.SetCurrentHeight("neo", "neo", _interopBlockHeight.ToString());
                            _interopBlockHeight++;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Neo block sync failed: " + e);
                }
                return result;
            }
        }

        private InteropBlock GetInteropBlock(BigInteger blockId)
        {
            var url = DomainExtensions.GetOracleBlockURL(
                "neo", "neo", blockId);

            return OracleReader.Read<InteropBlock>(DateTime.Now, url);
        }

        private List<Task<InteropBlock>> CreateTaskList(BigInteger batchCount, BigInteger[] blockIds = null)
        {
            List<Task<InteropBlock>> taskList = new List<Task<InteropBlock>>();
            if (blockIds == null)
            {
                var _interopBlockHeight = BigInteger.Parse(OracleReader.GetCurrentHeight("neo", "neo"));

                var nextCurrentBlockHeight = _interopBlockHeight + batchCount;

                for (var i = _interopBlockHeight; i < nextCurrentBlockHeight; i++)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            "neo", "neo", i);

                    taskList.Add(CreateTask(url));
                }
            }
            else
            {
                foreach (var blockId in blockIds)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            "neo", "neo", blockId);
                    taskList.Add(CreateTask(url));
                }
            }

            return taskList;
        }

        private Task<InteropBlock> CreateTask(string url)
        {
            return new Task<InteropBlock>(() =>
                   {
                       var delay = 1000;

                       while (true)
                       {
                           try
                           {
                               return OracleReader.Read<InteropBlock>(DateTime.Now, url);
                           }
                           catch (Exception e)
                           {
                               var logMessage = "oracleReader.Read() exception caught:\n" + e.Message;
                               var inner = e.InnerException;
                               while (inner != null)
                               {
                                   logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;
                                   inner = inner.InnerException;
                               }
                               logMessage += "\n\n" + e.StackTrace;

                               Log.Error(logMessage.Contains("Neo block is null") ? "oracleReader.Read(): Neo block is null, possible connection failure" : logMessage);
                           }

                           Thread.Sleep(delay);
                           if (delay >= 60000) // Once we reach 1 minute, we stop increasing delay and just repeat every minute.
                               delay = 60000;
                           else
                               delay *= 2;
                       }
                   });
        }

        private void ProcessBlock(InteropBlock block, List<PendingSwap> result)
        {
            foreach (var txHash in block.Transactions)
            {
                var interopTx = OracleReader.ReadTransaction("neo", "neo", txHash);

                if (interopTx.Transfers.Length == 0)
                {
                    continue;
                }

                InteropTransfer transfer;

                if (interopTx.Transfers.Length != 1)
                {
                    var sources = interopTx.Transfers.Select(x => x.sourceAddress).Distinct();
                    if (sources.Count() > 1)
                    {
                        throw new OracleException("neo transfers with multiple source addresses not supported yet");
                    }

                    var dests = interopTx.Transfers.Select(x => x.destinationAddress).Distinct();
                    if (dests.Count() > 1)
                    {
                        throw new OracleException("neo transfers with multiple destination addresses not supported yet");
                    }

                    var interops = interopTx.Transfers.Select(x => x.interopAddress).Distinct();
                    if (interops.Count() > 1)
                    {
                        throw new OracleException("neo transfers with multiple interop addresses not supported yet");
                    }

                    var symbols = interopTx.Transfers.Select(x => x.Symbol).Distinct();

                    if (symbols.Count() > 1)
                    {
                        throw new OracleException("neo transfers with multiple tokens not supported yet");
                    }

                    BigInteger sum = 0;

                    foreach (var temp in interopTx.Transfers)
                    {
                        sum += temp.Value;
                    }

                    var first = interopTx.Transfers.First();

                    if (first.Data != null && first.Data.Length > 0)
                    {
                        throw new OracleException("neo transfers with custom data are not supported yet");
                    }

                    transfer = new InteropTransfer(first.sourceChain, first.sourceAddress, first.destinationChain, first.destinationAddress, first.interopAddress, first.Symbol, sum);
                }
                else
                {
                    transfer = interopTx.Transfers.First();
                }

                if (transfer.sourceAddress == transfer.destinationAddress) // ignore this tx, this is a utxo consolidation
                {
                    continue;
                }

                result.Add(
                            new PendingSwap(
                                                this.PlatformName
                                            ,txHash
                                            , transfer.sourceAddress
                                            , transfer.interopAddress)
                        );
            }
        }

        private static string FindSymbolFromAsset(string assetID)
        {
            if (assetID.StartsWith("0x"))
            {
                assetID = assetID.Remove(0,2) ;
            }
            //Log.Debug("asset.... " + assetID);
            switch (assetID)
            {
                case "b3a766ac60afa2990d9251db08138fd1facf07ed": return "SOUL";
                case "ed07cffad18f1308db51920d99a2af60ac66a7b3": return "SOUL"; // ugly needs change
                case "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b": return "NEO";
                case "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7": return "GAS";
                default: return null;
            }
        }

        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(NeoBlock block,
                NeoAPI api, string[] swapAddresses, string coldStorage)
        {
            List<Hash> hashes = new List<Hash>();
            //Log.Debug($"Read block {block.Height} with hash {block.Hash}");

            // if the block has no swap tx, it's currently not of interest
            bool blockOfInterest = false;
            List<InteropTransaction> interopTransactions = new List<InteropTransaction>();
            foreach (var tx in block.Transactions)
            {
                if (tx.Type == TransactionType.InvocationTransaction
                    || tx.Type == TransactionType.ContractTransaction)
                {
                    var interopTx = MakeInteropTx(tx, api, swapAddresses, coldStorage);
                    if (interopTx.Hash != Hash.Null)
                    {
                        interopTransactions.Add(interopTx);
                        hashes.Add(Hash.FromBytes(tx.Hash.ToArray()));
                        blockOfInterest = true;
                    }
                }
            }

            InteropBlock iBlock = (blockOfInterest)
                ? new InteropBlock("neo", "neo", Hash.Parse(block.Hash.ToString()), hashes.ToArray())
                : new InteropBlock("neo", "neo", Hash.Null, hashes.ToArray());

            return Tuple.Create(iBlock, interopTransactions.ToArray());
        }

        public static InteropTransaction MakeInteropTx(NeoTx tx, NeoAPI api, string[] origSwapAddresses,
                string coldStorage)
        {
            Log.Debug("checking tx: " + tx.Hash);
            var swapAddresses = new List<Address>();
            foreach (var addr in origSwapAddresses)
            {
                swapAddresses.Add(NeoWallet.EncodeAddress(addr));
            }
            tx = (InvocationTransaction) tx;

            List<InteropTransfer> interopTransfers = new List<InteropTransfer>();

            var emptyTx = new InteropTransaction(Hash.Null, interopTransfers.ToArray());

            BigInteger amount;
            var witness = tx.Witnesses.ElementAtOrDefault(0);

            if (witness == null)
            {
                // tx has no witness
                return emptyTx;
            }

            var interopAddress = witness.ExtractAddress();
            if (tx.Witnesses.Length != 1 || interopAddress == Address.Null || interopAddress == Address.Null)
            {
                //currently only one witness allowed
                // if ExtractAddress returns Address.Null, the tx is not properly signed
                return emptyTx;
            }

            var sourceScriptHash = witness.VerificationScript.Sha256().RIPEMD160();
            var sourceAddress = NeoWallet.EncodeByteArray(sourceScriptHash);
            var sourceDecoded = NeoWallet.DecodeAddress(sourceAddress);

            if (sourceAddress == interopAddress || sourceDecoded == coldStorage)
            {
                Log.Warning("self send tx found, ignoring: " + tx.Hash);
                // self send, probably consolidation tx, ignore
                return emptyTx;
            }

            //Log.Debug("interop address: " + interopAddress);
            //Log.Debug("xswapAddress: " + swapAddress);
            //Log.Debug("interop sourceAddress: " + sourceAddress);
            //Log.Debug("neo sourceAddress: " + NeoWallet.DecodeAddress(sourceAddress));

            if (tx.Attributes != null && tx.Attributes.Length > 0)
            {
                foreach(var attr in tx.Attributes)
                {
                    if (attr.Usage == TransactionAttributeUsage.Description)
                    {
                        try
                        {
                            var text = Encoding.UTF8.GetString(attr.Data);
                            if (Address.IsValidAddress(text))
                            {
                                interopAddress = Address.FromText(text);
                                //Log.Debug("new interop address: " + interopAddress);
                            }
                        }
                        catch {}
                    }
                }
            }

            if (tx.Outputs.Length > 0)
            {
                foreach (var output in tx.Outputs)
                {
                    var targetAddress = NeoWallet.EncodeByteArray(output.ScriptHash.ToArray());
                    //Log.Debug("interop targetAddress : " + targetAddress);
                    //Log.Debug("neo targetAddress: " + NeoWallet.DecodeAddress(targetAddress));
                    //Log.Debug("targetAddress: " + targetAddress);

                    //Log.Debug("interop swpAddress: " + swpAddress);
                    //Log.Debug("neo swpAddress: " + NeoWallet.DecodeAddress(swpAddress));
                    //if (targetAddress.ToString() == swapAddress)
                    if (swapAddresses.Contains(targetAddress))
                    {
                        var token = FindSymbolFromAsset(new UInt256(output.AssetId.ToArray()).ToString());
                        CryptoCurrencyInfo tokenInfo;
                        if (NeoTokenInfo.TryGetValue(token, out tokenInfo))
                        {
                            amount = UnitConversion.ToBigInteger(
                                    (long)output.Value, tokenInfo.Decimals);
                        }
                        else
                        {
                            // asset not swapable at the moment...
                            //Log.Debug("Asset not swapable");
                            return emptyTx;
                        }

                        //Log.Debug("UTXO " + amount);
                        interopTransfers.Add
                        (
                            new InteropTransfer
                            (
                                NeoWallet.NeoPlatform,
                                sourceAddress,
                                DomainSettings.PlatformName,
                                targetAddress,
                                interopAddress, // interop address
                                token.ToString(),
                                amount
                            )
                        );
                    }
                }
            }

            if (tx.Type == TransactionType.InvocationTransaction)
            {

                var invocTx = tx as InvocationTransaction;

                if (invocTx.Script != null && invocTx.Script.Length > 0) // NEP5 transfers
                {
                    var script = NeoDisassembler.Disassemble(invocTx.Script, true);

                    if (script.lines.Count() < 7)
                    {
                        //Log.Debug("NO SCRIPT!!!!");
                        return emptyTx;
                    }

                    var disasmEntry = script.lines.ElementAtOrDefault(6);

                    //if ( disasmEntry == null )
                    //{
                    //    Log.Debug("disasmEntry is null");
                    //}
                    //if ( disasmEntry != null )
                    //{
                    //    if ( disasmEntry.data == null)
                    //        Log.Debug("disasmEntry.data is 0");
                    //}

                    if (disasmEntry.name != "APPCALL" || disasmEntry.data == null ||  disasmEntry.data.Length == 0)
                    {
                        //Log.Debug("NO APPCALL");
                        return emptyTx;
                    }
                    else
                    {

                        var assetString = new UInt160(disasmEntry.data).ToString();
                        if (string.IsNullOrEmpty(assetString) || FindSymbolFromAsset(assetString) == null)
                        {
                            //Log.Debug("Ignore TX due to non swapable token.");
                            return emptyTx;
                        }
                    }

                    int pos = 0;
                    foreach (var entry in script.lines)
                    {
                        pos++;
                        if (pos > 3)
                        {
                            // we are only interested in the first three elements
                            break;
                        }

                        if (pos == 1)
                        {
                            amount = new BigInteger(entry.data);
                        }
                        if (pos == 2 || pos == 3)
                        {
                            if (pos ==2)
                            {
                                var targetScriptHash = new UInt160(entry.data);
                                //Log.Debug("neo targetAddress: " + targetScriptHash.ToAddress());
                                var targetAddress = NeoWallet.EncodeByteArray(entry.data);
                                //Log.Debug("targetAddress : " + targetAddress);
                                //Log.Debug("SwapAddress: " + swapAddress);
                                if (swapAddresses.Contains(targetAddress))
                                {
                                    // found a swap, call getapplicationlog now to get transaction details and verify the tx was actually processed.
                                    ApplicationLog[] appLogs = null;
                                    try
                                    {

                                        appLogs = api.GetApplicationLog(invocTx.Hash);
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Error("Getting application logs failed: " + e.Message);
                                        return new InteropTransaction(Hash.Null, interopTransfers.ToArray());
                                    }

                                    if (appLogs != null)
                                    {
                                        for (var i = 0; i < appLogs.Length; i++)
                                        {
                                            //Log.Debug("appLogs[i].contract" + appLogs[i].contract);
                                            var token = FindSymbolFromAsset(appLogs[i].contract);
                                            //Log.Debug("TOKEN::::::::::::::::::: " + token);
                                            //Log.Debug("amount: " + appLogs[i].amount + " " + token);
                                            var sadd = NeoWallet.EncodeByteArray(appLogs[i].sourceAddress.ToArray());
                                            var tadd = NeoWallet.EncodeByteArray(appLogs[i].targetAddress.ToArray());


                                            interopTransfers.Add
                                            (
                                                new InteropTransfer
                                                (
                                                    "neo", // todo Pay.Chains.NeoWallet.NeoPlatform
                                                           //NeoWallet.EncodeByteArray(appLogs[i].sourceAddress.ToArray()),
                                                    sourceAddress,
                                                    DomainSettings.PlatformName,
                                                    targetAddress,
                                                    interopAddress, // interop address
                                                    token,
                                                    appLogs[i].amount
                                                )
                                            );
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("Neo swap is found but application log is not available for tx " + invocTx.Hash);
                                    }
                                }
                            }
                            else
                            {
                                //TODO reverse swap
                                sourceScriptHash = new UInt160(entry.data).ToArray();
                                sourceAddress = NeoWallet.EncodeByteArray(sourceScriptHash.ToArray());
                            }
                        }
                    }
                }
            }

            var total = interopTransfers.Count();
            if (total > 0)
            {
                Log.Information($"Found {total} swaps in neo tx {tx.Hash}");
            }
            else
            {
                Log.Debug($"No swaps in neo tx {tx.Hash}");
            }

            return ((interopTransfers.Count() > 0)
                ? new InteropTransaction(Hash.Parse(tx.Hash.ToString()), interopTransfers.ToArray())
                : new InteropTransaction(Hash.Null, interopTransfers.ToArray()));
        }


        private bool TxInBlockNeo(string txHash)
        {
            string strHeight;
            try
            {
                strHeight = this.neoAPI.GetTransactionHeight(txHash);
                Log.Debug("neo tx included in block: " + strHeight);
            }
            catch (Exception e)
            {
                Log.Error("Error during neo api call: " + e);
                return false;
            }

            int height;

            if (int.TryParse(strHeight, out height) && height > 0)
            {
                return true;
            }

            return false;
        }

        private Hash VerifyNeoTx(Hash sourceHash, string txHash)
        {
            var counter = 0;
            do
            {

                Thread.Sleep(15000); // wait 15 seconds

                if (txHash.StartsWith("0x"))
                {
                    txHash = txHash.Substring(2);
                }

                if (TxInBlockNeo(txHash))
                {
                    return Hash.Parse(txHash);
                }
                else
                {
                    counter++;
                    if (counter > 5)
                    {
                        string node = null;

                        var rpcMap = new StorageMap(TokenSwapper.UsedRpcTag, Swapper.Storage);
                        node = rpcMap.Get<Hash, string>(sourceHash);

                        // tx could still be in mempool
                        bool inMempool = true;
                        try
                        {
                            inMempool = this.neoAPI.CheckMempool(node, txHash);
                        }
                        catch (Exception e)
                        {
                            // If we can't check mempool, we are unable to verify if the tx has gone through or not,
                            // therefore we have to wait until we are able to check this nodes mempool again, or find
                            // the tx in a block in the next round.
                            Log.Error("Exception during mempool check: " + e);
                            return Hash.Null;
                        }

                        if (inMempool)
                        {
                            // tx still in mempool, do nothing
                            return Hash.Null;
                        }
                        else
                        {
                            // to make sure it wasn't moved out from mempool and is already processed check again if the tx is already added to a block
                            if (TxInBlockNeo(txHash))
                            {
                                return Hash.Parse(txHash);
                            }
                            else
                            {
                                // tx is neither in a block, nor in mempool, either dropped out of mempool or mempool was full already
                                Log.Error($"Possible failed neo swap sourceHash: {sourceHash} txHash: {txHash}");
                            }
                        }
                        return Hash.Null;
                    }
                }

            } while (true);
        }

        internal override Hash VerifyExternalTx(Hash sourceHash, string txStr)
        {
            return VerifyNeoTx(sourceHash, txStr);
        }


        // NOTE no locks happen here because this callback is called from within a lock
        internal override Hash SettleSwap(Hash sourceHash, Address destination, IToken token, BigInteger amount)
        {
            Hash txHash = Hash.Null;
            string txStr = null;

            var inProgressMap = new StorageMap(TokenSwapper.InProgressTag, Swapper.Storage);
            var rpcMap = new StorageMap(TokenSwapper.UsedRpcTag, Swapper.Storage);

            if (inProgressMap.ContainsKey<Hash>(sourceHash))
            {
                txStr = inProgressMap.Get<Hash, string>(sourceHash);

                if (!string.IsNullOrEmpty(txStr))
                {
                    return VerifyNeoTx(sourceHash, txStr);
                }
            }

            var total = UnitConversion.ToDecimal(amount, token.Decimals);

            var neoKeys = NeoKeys.FromWIF(this.WIF);

            var destAddress = NeoWallet.DecodeAddress(destination);

            Log.Debug($"NEOSWAP: Trying transfer of {total} {token.Symbol} from {neoKeys.Address} to {destAddress}");

            var nonce = sourceHash.ToByteArray();

            NeoTx tx = null;
            string usedRpc = null;
            try
            {
                if (token.Symbol == "NEO" || token.Symbol == "GAS")
                {
                    tx = neoAPI.SendAsset(neoKeys, destAddress, token.Symbol, total, out usedRpc);
                }
                else
                {
                    var nep5 = neoAPI.GetToken(token.Symbol);
                    tx = nep5.Transfer(neoKeys, destAddress, total, nonce, x => usedRpc = x);
                }

                // persist resulting tx hash as in progress
                inProgressMap.Set<Hash, string>(sourceHash, tx.Hash.ToString());
                rpcMap.Set<Hash, string>(sourceHash, usedRpc);

                Log.Debug("broadcasted neo tx: " + tx);
            }
            catch (Exception e)
            {
                Log.Error("Error during transfering {token.Symbol}: " + e);
                return Hash.Null;
            }

            if (tx == null)
            {
                Log.Error($"NeoAPI error {neoAPI.LastError} or possible failed neo swap sourceHash: {sourceHash} no transfer happend.");
                return Hash.Null;
            }

            var strHash = tx.Hash.ToString();

            return VerifyNeoTx(sourceHash, strHash);
        }
    }
}
