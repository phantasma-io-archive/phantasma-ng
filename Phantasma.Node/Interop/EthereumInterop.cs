using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Node.Chains.Ethereum;
using Phantasma.Shared.Utils;
using Serilog;
using EthereumKey = Phantasma.Node.Chains.Ethereum.EthereumKey;

namespace Phantasma.Node.Interop
{
    public class EthereumInterop: ChainSwapper
    {
        private EthAPI ethAPI;
        private List<string> contracts;
        private uint confirmations;
        private List<BigInteger> _resyncBlockIds = new List<BigInteger>();
        private static bool initialStart = true;

        public EthereumInterop(TokenSwapper swapper, EthAPI ethAPI, BigInteger interopBlockHeight, string[] contracts, uint confirmations)
                : base(swapper, EthereumWallet.EthereumPlatform)
        {
            string lastBlockHeight = OracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform);
            if(string.IsNullOrEmpty(lastBlockHeight))
                OracleReader.SetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, interopBlockHeight.ToString());

            Log.Information($"interopHeight: {OracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform)}");

            this.contracts = contracts.ToList();

            // add local swap address to contracts
            this.contracts.Add(LocalAddress);

            this.confirmations = confirmations;
            this.ethAPI = ethAPI;
        }

        protected override string GetAvailableAddress(string wif)
        {
            var ethKeys = EthereumKey.FromWIF(wif);
            return ethKeys.Address;
        }

        public override IEnumerable<PendingSwap> Update()
        {
            // wait another 10s to execute eth interop
            //Task.Delay(10000).Wait();
            try
            {
                lock (String.Intern("PendingSetCurrentHeight_" + EthereumWallet.EthereumPlatform))
                {
                    var result = new List<PendingSwap>();

                    // initial start, we have to verify all processed swaps
                    if (initialStart)
                    {
                        Log.Debug($"Read all ethereum blocks now.");
                        var allInteropBlocks = OracleReader.ReadAllBlocks(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform);

                        Log.Debug($"Found {allInteropBlocks.Count} blocks");

                        foreach (var block in allInteropBlocks)
                        {
                            ProcessBlock(block, ref result);
                        }

                        initialStart = false;

                        // return after the initial start to be able to process all swaps that happend in the mean time.
                        return result;
                    }

                    var currentHeight = ethAPI.GetBlockHeight();
                    var _interopBlockHeight = BigInteger.Parse(OracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform));
                    Log.Debug($"Swaps: Current Eth chain height: {currentHeight}, interop: {_interopBlockHeight}, delta: {currentHeight - _interopBlockHeight}");

                    var blocksProcessedInOneBatch = 0;
                    while (blocksProcessedInOneBatch < 50)
                    {
                        if (_resyncBlockIds.Any())
                        {
                            for (var i = 0; i < _resyncBlockIds.Count; i++)
                            {
                                var blockId = _resyncBlockIds.ElementAt(i);
                                if (blockId > _interopBlockHeight)
                                {
                                    Log.Warning($"EthInterop:Update() resync block {blockId} higher than current interop height, can't resync.");
                                    _resyncBlockIds.RemoveAt(i);
                                    continue;
                                }

                                try
                                {
                                    Log.Debug($"EthInterop:Update() resync block {blockId} now.");
                                    var block = GetInteropBlock(blockId);
                                    ProcessBlock(block, ref result);
                                }
                                catch (Exception e)
                                {
                                    Log.Error($"EthInterop:Update() resync block {blockId} failed: " + e);
                                }
                                _resyncBlockIds.RemoveAt(i);
                            }
                        }

                        blocksProcessedInOneBatch++;

                        var blockDifference = currentHeight - _interopBlockHeight;
                        if (blockDifference < confirmations)
                        {
                            // no need to query the node yet
                            break;
                        }

                        //TODO quick sync not done yet, requieres a change to the oracle impl to fetch multiple blocks
                        //var nextHeight = (blockDifference > 50) ? 50 : blockDifference; //TODO

                        //var transfers = new Dictionary<string, Dictionary<string, List<InteropTransfer>>>();

                        //if (nextHeight > 1)
                        //{
                        //    var blockCrawler = new EthBlockCrawler(contracts.ToArray(), 0/*confirmations*/, ethAPI); //TODO settings confirmations

                        //    blockCrawler.Fetch(currentHeight, nextHeight);
                        //    transfers = blockCrawler.ExtractInteropTransfers(LocalAddress);
                        //    foreach (var entry in transfers)
                        //    {
                        //        foreach (var txInteropTransfer in entry.Value)
                        //        {
                        //            foreach (var interopTransfer in txInteropTransfer.Value)
                        //            {
                        //                result.Add(new PendingSwap(
                        //                    this.PlatformName
                        //                    ,Hash.Parse(entry.Key)
                        //                    ,interopTransfer.sourceAddress
                        //                    ,interopTransfer.interopAddress)
                        //                );
                        //            }
                        //        }
                        //    }

                        //    _interopBlockHeight = nextHeight;
                        //    oracleReader.SetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, _interopBlockHeight.ToString());
                        //}
                        //else
                        //{

                        /* Future improvement, implement oracle call to fetch multiple blocks */
                        var url = DomainExtensions.GetOracleBlockURL(
                                EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, _interopBlockHeight);

                        var interopBlock = OracleReader.Read<InteropBlock>(DateTime.Now, url);

                        ProcessBlock(interopBlock, ref result);

                        _interopBlockHeight++;
                        //}
                    }

                    OracleReader.SetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, _interopBlockHeight.ToString());

                    var total = result.Count();
                    if (total > 0)
                    {
                        Log.Information($"found {total} ethereum swaps");
                    }
                    else
                    {
                        Log.Debug($"did not find any ethereum swaps");
                    }
                    return result;
                }
            }
            catch (Exception e)
            {
                var logMessage = "EthereumInterop.Update() exception caught:\n" + e.Message;
                var inner = e.InnerException;
                while (inner != null)
                {
                    logMessage += "\n---> " + inner.Message + "\n\n" + inner.StackTrace;
                    inner = inner.InnerException;
                }
                logMessage += "\n\n" + e.StackTrace;

                Log.Error(logMessage);

                return new List<PendingSwap>();
            }
        }

        TimeSpan TimeAction(Action blockingAction)
        {
            Stopwatch stopWatch = System.Diagnostics.Stopwatch.StartNew();
            blockingAction();
            stopWatch.Stop();
            return stopWatch.Elapsed;
        }


        public override void ResyncBlock(BigInteger blockId)
        {
            lock(_resyncBlockIds)
            {
                _resyncBlockIds.Add(blockId);
            }
        }

        private List<Task<InteropBlock>> CreateTaskList(BigInteger batchCount, BigInteger currentHeight, BigInteger[] blockIds = null)
        {
            List<Task<InteropBlock>> taskList = new List<Task<InteropBlock>>();
            if (blockIds == null)
            {
                var _interopBlockHeight = BigInteger.Parse(OracleReader.GetCurrentHeight(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform));
                var nextCurrentBlockHeight = _interopBlockHeight + batchCount;

                if (nextCurrentBlockHeight > currentHeight)
                {
                    nextCurrentBlockHeight = currentHeight;
                }
                
                for (var i = _interopBlockHeight; i <= nextCurrentBlockHeight; i++)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, i);
                
                    taskList.Add(CreateTask(url));
                }
            }
            else
            {
                foreach (var blockId in blockIds)
                {
                    var url = DomainExtensions.GetOracleBlockURL(
                            EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, blockId);
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

                               Log.Error(logMessage.Contains("Ethereum block is null") ? "oracleReader.Read(): Ethereum block is null, possible connection failure" : logMessage);
                           }

                           Thread.Sleep(delay);
                           if (delay >= 60000) // Once we reach 1 minute, we stop increasing delay and just repeat every minute.
                               delay = 60000;
                           else
                               delay *= 2;
                       }
                   });
        }

        private void ProcessBlock(InteropBlock block, ref List<PendingSwap> result)
        {
            foreach (var txHash in block.Transactions)
            {
                var interopTx = OracleReader.ReadTransaction(EthereumWallet.EthereumPlatform, "ethethereum", txHash);

                foreach (var interopTransfer in interopTx.Transfers)
                {
                    result.Add(
                                new PendingSwap(
                                                 this.PlatformName
                                                ,txHash
                                                ,interopTransfer.sourceAddress
                                                ,interopTransfer.interopAddress)
                            );
                }
            }
        }

        private InteropBlock GetInteropBlock(BigInteger blockId)
        {
            var url = DomainExtensions.GetOracleBlockURL(
                EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, blockId);

            return OracleReader.Read<InteropBlock>(DateTime.Now, url);
        }


        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(BlockWithTransactions block, EthAPI api
                , string[] swapAddress)
        {
            //TODO
            return null;
        }

        public static Address ExtractInteropAddress(Nethereum.RPC.Eth.DTOs.Transaction tx)
        {
            //Using the transanction from RPC to build a txn for signing / signed
            var transaction = Nethereum.Signer.TransactionFactory.CreateTransaction(tx.To, tx.Gas, tx.GasPrice, tx.Value, tx.Input, tx.Nonce,
                tx.R, tx.S, tx.V);
            
            //Get the account sender recovered
            Nethereum.Signer.EthECKey accountSenderRecovered = null;
            if (transaction is Nethereum.Signer.TransactionChainId)
            {
                var txnChainId = transaction as Nethereum.Signer.TransactionChainId;
                accountSenderRecovered = Nethereum.Signer.EthECKey.RecoverFromSignature(transaction.Signature, transaction.RawHash, txnChainId.GetChainIdAsBigInteger());
            }
            else
            {
                accountSenderRecovered = Nethereum.Signer.EthECKey.RecoverFromSignature(transaction.Signature, transaction.RawHash);
            }

            var pubKey = accountSenderRecovered.GetPubKey();
            var ecCurve = Org.BouncyCastle.Asn1.Sec.SecNamedCurves.GetByName("secp256k1");
            var q = ecCurve.Curve.DecodePoint(pubKey);
            var dom = new Org.BouncyCastle.Crypto.Parameters.ECDomainParameters(ecCurve.Curve, ecCurve.G, ecCurve.N, ecCurve.H);
            var publicParams = new Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(q, dom);
            pubKey = publicParams.Q.GetEncoded(true);

            var bytes = new byte[34];
            bytes[0] = (byte)AddressKind.User;
            ByteArrayUtils.CopyBytes(pubKey, 0, bytes, 1, 33);

            return Address.FromBytes(bytes);
        }

        public static Tuple<InteropBlock, InteropTransaction[]> MakeInteropBlock(Nexus nexus, EthAPI api
                , BigInteger height, string[] contracts, uint confirmations, string[] swapAddress)
        {
            Hash blockHash = Hash.Null;
            var interopTransactions = new List<InteropTransaction>();

            //TODO HACK
            var combinedAddresses = contracts.ToList();
            combinedAddresses.AddRange(swapAddress);

            Dictionary<string, Dictionary<string, List<InteropTransfer>>> transfers = new Dictionary<string, Dictionary<string, List<InteropTransfer>>>();
            try
            {
                var crawler = new EthBlockCrawler(combinedAddresses.ToArray(), confirmations, api);
                // fetch blocks
                crawler.Fetch(height);
                transfers = crawler.ExtractInteropTransfers(nexus, swapAddress);
            }
            catch (Exception e)
            {
                Log.Error("Failed to fetch eth blocks: " + e);
            }

            if (transfers.Count == 0)
            {
                var emptyBlock =  new InteropBlock(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, Hash.Null, new Hash[]{});
                return Tuple.Create(emptyBlock, interopTransactions.ToArray());
            }

            blockHash = Hash.Parse(transfers.FirstOrDefault().Key);

            foreach (var block in transfers)
            {
                var txTransferDict  = block.Value;
                foreach (var tx in txTransferDict)
                {
                    var interopTx = MakeInteropTx(tx.Key, tx.Value);
                    if (interopTx.Hash != Hash.Null)
                    {
                        interopTransactions.Add(interopTx);
                    }
                }
            }

            var hashes = interopTransactions.Select(x => x.Hash).ToArray() ;

            InteropBlock interopBlock = (interopTransactions.Count() > 0)
                ? new InteropBlock(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, blockHash, hashes)
                : new InteropBlock(EthereumWallet.EthereumPlatform, EthereumWallet.EthereumPlatform, Hash.Null, hashes);

            return Tuple.Create(interopBlock, interopTransactions.ToArray());
        }

        private static string FetchTokenURI(string contractAddress, BigInteger tokenID)
        {
            throw new NotImplementedException();
            /*
            Nethereum.Web3.Web3 web3 = null; ????
            abi = ??
            var contract = web3.Eth.GetContract(abi, contractAddress);
            var function = contract.GetFunction("tokenURI");
            object[] args = new object[] { tokenID };
            var result = function.CallAsync<string>(args);
            return result;*/
        }

        private static Dictionary<string, List<InteropTransfer>> GetInteropTransfers(Nexus nexus,
                TransactionReceipt txr, EthAPI api, string[] swapAddresses)
        {
            Log.Debug($"get interop transfers for tx {txr.TransactionHash}");
            var interopTransfers = new Dictionary<string, List<InteropTransfer>>();

            Nethereum.RPC.Eth.DTOs.Transaction tx = null;
            try
            {
                // tx to get the eth transfer if any
                tx = api.GetTransaction(txr.TransactionHash);
            }
            catch (Exception e)
            {
                Log.Error("Getting eth tx failed: " + e.Message);
            }

            Log.Debug("Transaction status: " + txr.Status.Value);
            // check if tx has failed
            if (txr.Status.Value == 0)
            {
                Log.Error($"tx {txr.TransactionHash} failed");
                return interopTransfers;
            }

            var nodeSwapAddresses = swapAddresses.Select(x => EthereumWallet.EncodeAddress(x));
            var interopAddress = ExtractInteropAddress(tx);

            // ERC721 (NFT)
            // TODO currently this code block is mostly copypaste from ERC20 block, later make a single method for both...
            var erc721_events = txr.DecodeAllEvents<Nethereum.StandardNonFungibleTokenERC721.ContractDefinition.TransferEventDTOBase>();
            foreach (var evt in erc721_events)
            {
                var asset = EthUtils.FindSymbolFromAsset(nexus, evt.Log.Address);
                if (asset == null)
                {
                    Log.Warning($"Asset [{evt.Log.Address}] not supported");
                    continue;
                }

                var targetAddress = EthereumWallet.EncodeAddress(evt.Event.To);
                var sourceAddress = EthereumWallet.EncodeAddress(evt.Event.From);
                var tokenID = BigInteger.Parse(evt.Event.TokenId.ToString());

                if (nodeSwapAddresses.Contains(targetAddress))
                {
                    if (!interopTransfers.ContainsKey(evt.Log.TransactionHash))
                    {
                        interopTransfers.Add(evt.Log.TransactionHash, new List<InteropTransfer>());
                    }

                    string tokenURI = FetchTokenURI(evt.Log.Address, evt.Event.TokenId);

                    interopTransfers[evt.Log.TransactionHash].Add
                    (
                        new InteropTransfer
                        (
                            EthereumWallet.EthereumPlatform,
                            sourceAddress,
                            DomainSettings.PlatformName,
                            targetAddress,
                            interopAddress,
                            asset,
                            tokenID,
                            System.Text.Encoding.UTF8.GetBytes(tokenURI)
                        )
                    );
                }
            }

            // ERC20
            var erc20_events = txr.DecodeAllEvents<TransferEventDTO>();
            foreach (var evt in erc20_events)
            {
                var asset = EthUtils.FindSymbolFromAsset(nexus, evt.Log.Address);
                if (asset == null)
                {
                    Log.Warning($"Asset [{evt.Log.Address}] not supported");
                    continue;
                }

                var targetAddress = EthereumWallet.EncodeAddress(evt.Event.To);
                var sourceAddress = EthereumWallet.EncodeAddress(evt.Event.From);
                var amount = BigInteger.Parse(evt.Event.Value.ToString());

                if (nodeSwapAddresses.Contains(targetAddress))
                {
                    if (!interopTransfers.ContainsKey(evt.Log.TransactionHash))
                    {
                        interopTransfers.Add(evt.Log.TransactionHash, new List<InteropTransfer>());
                    }

                    interopTransfers[evt.Log.TransactionHash].Add
                    (
                        new InteropTransfer
                        (
                            EthereumWallet.EthereumPlatform,
                            sourceAddress,
                            DomainSettings.PlatformName,
                            targetAddress,
                            interopAddress,
                            asset,
                            amount
                        )
                    );
                }
            }

            if (tx.Value != null && tx.Value.Value > 0)
            {
                var targetAddress = EthereumWallet.EncodeAddress(tx.To);
                var sourceAddress = EthereumWallet.EncodeAddress(tx.From);

                if (nodeSwapAddresses.Contains(targetAddress))
                {
                    var amount = BigInteger.Parse(tx.Value.ToString());

                    if (!interopTransfers.ContainsKey(tx.TransactionHash))
                    {
                        interopTransfers.Add(tx.TransactionHash, new List<InteropTransfer>());
                    }

                    interopTransfers[tx.TransactionHash].Add
                    (
                        new InteropTransfer
                        (
                            EthereumWallet.EthereumPlatform,
                            sourceAddress,
                            DomainSettings.PlatformName,
                            targetAddress,
                            interopAddress,
                            "ETH", // TODO use const
                            amount
                        )
                    );
                }
            }


            return interopTransfers;
        }

        public static InteropTransaction MakeInteropTx(string txHash, List<InteropTransfer> transfers)
        {
            return ((transfers.Count() > 0)
                ? new InteropTransaction(Hash.Parse(txHash), transfers.ToArray())
                : new InteropTransaction(Hash.Null, transfers.ToArray()));
        }

        public static InteropTransaction MakeInteropTx(Nexus nexus, TransactionReceipt txr, EthAPI api, string[] swapAddresses)
        {
            Log.Debug("checking tx: " + txr.TransactionHash);

            IList<InteropTransfer> interopTransfers = new List<InteropTransfer>();

            interopTransfers = GetInteropTransfers(nexus, txr, api, swapAddresses).SelectMany(x => x.Value).ToList();
            Log.Debug($"Found {interopTransfers.Count} interop transfers!");

            return ((interopTransfers.Count() > 0)
                ? new InteropTransaction(Hash.Parse(txr.TransactionHash), interopTransfers.ToArray())
                : new InteropTransaction(Hash.Null, interopTransfers.ToArray()));

        }

        public static decimal GetNormalizedFee(FeeUrl[] fees)
        {
            var taskList = new List<Task<decimal>>();

            foreach (var fee in fees)
            {
                taskList.Add(
                        new Task<decimal>(() => 
                        {
                            return GetFee(fee);
                        })
                );
            }

            Parallel.ForEach(taskList, (task) =>
            {
                task.Start();
            });

            Task.WaitAll(taskList.ToArray());

            var results = new List<decimal>();
            foreach (var task in taskList)
            {
                results.Add(task.Result);
            }

            var median = GetMedian<decimal>(results.ToArray());

            return median;
        }

        public static decimal GetFee(FeeUrl feeObj)
        {
            decimal fee = 0;

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = httpClient.GetAsync(feeObj.url).Result;
                    using (var content = response.Content)
                    {
                        var json = content.ReadAsStringAsync().Result;
                        var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty(feeObj.feeHeight, out var prop))
                        {
                            fee = decimal.Parse(prop.ToString().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                            fee += feeObj.feeIncrease;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Getting fee failed: " + e);
            }

            return fee;
        }

        public static T GetMedian<T>(T[] sourceArray) where T : IComparable<T>
        {
            if (sourceArray == null || sourceArray.Length == 0)
                throw new ArgumentException("Median of empty array not defined.");

            T[] sortedArray = sourceArray;
            Array.Sort(sortedArray);

            //get the median
            int size = sortedArray.Length;
            int mid = size / 2;
            if (size % 2 != 0)
            {
                return sortedArray[mid];
            }

            dynamic value1 = sortedArray[mid];
            dynamic value2 = sortedArray[mid - 1];

            return (sortedArray[mid] + value2) * 0.5;
        }

        private Hash VerifyEthTx(Hash sourceHash, string txHash)
        {
            TransactionReceipt txr;

            try
            {
                var retries = 0;
                do
                {
                    // Checking if tx is mined.
                    txr = ethAPI.GetTransactionReceipt(txHash);
                    if (txr == null)
                    {
                        if (retries == 12) // Waiting for 1 minute max.
                            break;

                        Thread.Sleep(5000);
                        retries++;
                    }
                } while (txr == null);

                if (txr == null)
                {
                    Log.Error($"Ethereum transaction {txHash} not mined yet.");
                    return Hash.Null;
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception during polling for receipt: {e}");
                return Hash.Null;
            }

            if (txr.Status.Value == 0) // Status == 0 = error
            {
                Log.Error($"Possible failed eth swap sourceHash: {sourceHash} txHash: {txHash}");

                Log.Error($"EthAPI error, tx {txr.TransactionHash} ");
                return Hash.Null;
            }

            return Hash.Parse(txr.TransactionHash.Substring(2));
        }

        internal override Hash VerifyExternalTx(Hash sourceHash, string txStr)
        {
            return VerifyEthTx(sourceHash, txStr);
        }



        // NOTE no locks happen here because this callback is called from within a lock
        internal override Hash SettleSwap(Hash sourceHash, Address destination, IToken token, BigInteger amount)
        {
            // check if tx was sent but not minded yet
            string tx = null;

            var inProgressMap = new StorageMap(TokenSwapper.InProgressTag, Swapper.Storage);

            if (inProgressMap.ContainsKey<Hash>(sourceHash))
            {
                tx = inProgressMap.Get<Hash, string>(sourceHash);

                if (!string.IsNullOrEmpty(tx))
                {
                    return VerifyEthTx(sourceHash, tx);
                }
            }

            var total = UnitConversion.ToDecimal(amount, token.Decimals);

            var ethKeys = EthereumKey.FromWIF(this.WIF);

            var destAddress = EthereumWallet.DecodeAddress(destination);

            try
            {
                Log.Debug($"ETHSWAP: Trying transfer of {total} {token.Symbol} from {ethKeys.Address} to {destAddress}");
                var transferResult = ethAPI.TryTransferAsset(token.Symbol, destAddress, total, token.Decimals, out tx);

                if (transferResult == EthTransferResult.Success)
                {
                    // persist resulting tx hash as in progress
                    inProgressMap.Set<Hash, string>(sourceHash, tx);
                    Log.Debug("broadcasted eth tx: " + tx);
                }
                else
                {
                    Log.Error($"ETHSWAP: Transfer of {total} {token.Symbol} from {ethKeys.Address} to {destAddress} failed, no tx generated");
                }
            }
            catch (Exception e)
            {
                Log.Error($"Exception during transfer: {e}");
                // we don't know if the transfer happend or not, therefore can't delete from inProgressMap yet.
                return Hash.Null;
            }

            return VerifyEthTx(sourceHash, tx);
        }

    }
}
