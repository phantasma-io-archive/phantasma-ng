using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using Nethereum.BlockchainProcessing;
using Nethereum.BlockchainProcessing.Processor;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;

using Phantasma.Core;
using Phantasma.Business;

using Phantasma.Infrastructure.Chains;

using Phantasma.Spook.Interop;

using InteropTransfers = System.Collections.Generic.Dictionary<string,
      System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Phantasma.Core.InteropTransfer>>>;
using System.Linq;
using Serilog.Core;

namespace Phantasma.Spook.Chains
{
    public class CrawledBlock
    {
        public Hash Hash { get; }
        public InteropTransfers Transfers { get; }

        public CrawledBlock(Hash hash, InteropTransfers transfers)
        {
            Hash = hash;
            Transfers = transfers;
        }
    }

    public class EthBlockCrawler
    {
        private string[] addressesToWatch;
        private BlockchainProcessor processor;
        private CancellationToken cancellationToken;
        private List<TransactionReceiptVO> transactions = new List<TransactionReceiptVO>();
        private Web3 web3;
        private Logger logger;

        public List<TransactionReceiptVO> Result => transactions;

        public EthBlockCrawler(Logger logger, string[] addresses, uint blockConfirmations, EthAPI api)
        {
            this.addressesToWatch = addresses;
            this.web3 = api.GetWeb3Client();
            this.logger = logger;

            processor = web3.Processing.Blocks.CreateBlockProcessor(steps =>
                {
                    steps.TransactionStep.SetMatchCriteria(t => t.Transaction.IsToAny(addressesToWatch));
                    steps.TransactionReceiptStep.AddSynchronousProcessorHandler(tx => AddTxrVO(tx));
               }, 
               blockConfirmations // block confirmations count
            );
            cancellationToken = new CancellationToken();
        }

        private void AddTxrVO(TransactionReceiptVO txr)
        {
            lock (transactions)
            {
                transactions.Add(txr);
            }
        }

        public void Fetch(BigInteger height)
        {
            Fetch(height, BigInteger.Zero);
        }

        public void Fetch(BigInteger from, BigInteger to)
        {
            EthUtils.RunSync(() => 
                processor.ExecuteAsync(
                        startAtBlockNumberIfNotProcessed: from,
                        toBlockNumber: (to != BigInteger.Zero) ? to : from,
                        cancellationToken: cancellationToken)
                    );
        }

        public InteropTransfers ExtractInteropTransfers(Business.Nexus nexus, Logger logger, string[] swapAddresses)
        {
            var interopTransfers = new InteropTransfers();
            lock (transactions)
            {
                foreach(var txVo in transactions)
                {
                    var block = txVo.Block;
                    var txr = txVo.TransactionReceipt;
                    var tx = txVo.Transaction;

                    var interopAddress = EthereumInterop.ExtractInteropAddress(tx);
                    var transferEvents = txr.DecodeAllEvents<TransferEventDTO>();
                    //var swapEvents = txr.DecodeAllEvents<SwapEventDTO>();
                    var nodeSwapAddresses = swapAddresses.Select(x => EthereumWallet.EncodeAddress(x)).ToList();
                    //var nodeSwapAddresses = EthereumWallet.EncodeAddress(swapAddress);

                    if (transferEvents.Count > 0 || tx.Value != null && tx.Value.Value > 0)
                    {
                        if (!interopTransfers.ContainsKey(block.BlockHash))
                        {
                            interopTransfers.Add(block.BlockHash, new Dictionary<string, List<InteropTransfer>>());
                        }
                    }

                    if (transferEvents.Count > 0)
                    {
                        var blockId = block.Number.ToString();
                        var hash = txr.TransactionHash;

                        foreach(var evt in transferEvents)
                        {
                            var targetAddress = EthereumWallet.EncodeAddress(evt.Event.To);

                            // If it's not our address, skip immediatly, don't log it
                            if (!nodeSwapAddresses.Contains(targetAddress))
                            {
                                continue;
                            }

                            logger.Information($"Found ERC20 swap: {blockId} hash: {hash} to: {evt.Event.To} from: {evt.Event.From} value: {evt.Event.Value}");
                            var asset = EthUtils.FindSymbolFromAsset(nexus, evt.Log.Address);
                            logger.Information("asset: " + asset);
                            if (asset == null)
                            {
                                logger.Information($"Asset [{evt.Log.Address}] not supported");
                                continue;
                            }

                            
                            var sourceAddress = EthereumWallet.EncodeAddress(evt.Event.From);
                            var amount = BigInteger.Parse(evt.Event.Value.ToString());

                            //logger.Message("nodeSwapAddress: " + nodeSwapAddress);
                            logger.Information("sourceAddress: " + sourceAddress);
                            logger.Information("targetAddress: " + targetAddress);
                            logger.Information("amount: " + amount);

                            if (!interopTransfers[block.BlockHash].ContainsKey(evt.Log.TransactionHash))
                            {
                                interopTransfers[block.BlockHash].Add(evt.Log.TransactionHash, new List<InteropTransfer>());
                            }

                            interopTransfers[block.BlockHash][evt.Log.TransactionHash].Add
                                (
                                 new InteropTransfer
                                 (
                                  EthereumWallet.EthereumPlatform,
                                  sourceAddress,
                                  DomainSettings.PlatformName,
                                  targetAddress,
                                  interopAddress, // interop address
                                  asset,
                                  amount
                                 )
                                );
                        }
                    }

                    if (tx.Value != null && tx.Value.Value > 0)
                    {
                        logger.Information("ETH:");
                        logger.Information(block.Number.ToString());
                        logger.Information(tx.TransactionHash);
                        logger.Information(tx.To);
                        logger.Information(tx.From);
                        logger.Information(tx.Value.ToString());

                        var targetAddress = EthereumWallet.EncodeAddress(tx.To);

                        if (!nodeSwapAddresses.Contains(targetAddress ))
                        {
                            continue;
                        }

                        if (!interopTransfers[block.BlockHash].ContainsKey(tx.TransactionHash))
                        {
                            interopTransfers[block.BlockHash].Add(tx.TransactionHash, new List<InteropTransfer>());
                        }

                        var sourceAddress = EthereumWallet.EncodeAddress(tx.From);
                        var amount = BigInteger.Parse(tx.Value.ToString());

                        interopTransfers[block.BlockHash][tx.TransactionHash].Add
                            (
                             new InteropTransfer
                             (
                              EthereumWallet.EthereumPlatform,
                              sourceAddress,
                              DomainSettings.PlatformName,
                              targetAddress,
                              interopAddress, // interop address
                              "ETH", // TODO use const
                              amount
                             )
                            );
                    }
                }

                transactions.Clear();
            }

            // clear transactions after extraction was done
            return interopTransfers;
        }
    }
}
