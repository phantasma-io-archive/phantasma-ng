using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.StandardTokenEIP20.ContractDefinition;

using Phantasma.Core;
using Phantasma.Infrastructure;
using Transaction = Nethereum.RPC.Eth.DTOs.Transaction;

namespace Phantasma.Node.Chains
{
    public enum EthTransferResult
    {
        Failure,
        Success
    }

    public class EthBlockIterator
    {
        public BigInteger currentBlock;
        public uint currentTransaction;

        public EthBlockIterator(EthAPI api)
        {
            this.currentBlock = api.GetBlockHeight();
            this.currentTransaction = 0;
        }

        public override string ToString()
        {
            return $"{currentBlock}/{currentTransaction}";
        }
    }

    public class EthereumException : Exception
    {
        public EthereumException(string msg) : base(msg)
        {

        }

        public EthereumException(string msg, Exception cause) : base(msg, cause)
        {

        }
    }

    public class EthAPI
    {
        public string LastError { get; protected set; }
        private List<string> urls = new List<string>();
        private List<Web3> web3Clients = new List<Web3>();
        private Account _account;

        private static Random rnd = new Random();

        public EthAPI(Account account)
        {
            this._account = account;

            this.urls = Settings.Default.Oracle.EthRpcNodes;
            if (this.urls.Count == 0)
            {
                throw new ArgumentNullException("Need at least one RPC node");
            }

            foreach (var url in this.urls)
            {
                web3Clients.Add(new Web3(_account, url));
            }
        }

        public BigInteger GetBlockHeight()
        {
            var height = Task.Run(async() => await GetWeb3Client().Eth.Blocks.GetBlockNumber.SendRequestAsync()).Result.Value;
            return height;
        }

        public BlockWithTransactions GetBlock(BigInteger height)
        {
            return GetBlock(new HexBigInteger(height));
        }

        public BlockWithTransactions GetBlock(HexBigInteger height)
        {
            return EthUtils.RunSync(() => GetWeb3Client().Eth.Blocks.GetBlockWithTransactionsByNumber
                    .SendRequestAsync(new BlockParameter(height)));

        }

        public BlockWithTransactions GetBlock(string hash)
        {
            return EthUtils.RunSync(() => GetWeb3Client().Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(hash));
        }

        public Transaction GetTransaction(string tx)
        {
            var transaction = EthUtils.RunSync(() => GetWeb3Client().Eth.Transactions.GetTransactionByHash.SendRequestAsync(tx));
            return transaction;
        }

        public TransactionReceipt GetTransactionReceipt(string tx)
        {
            TransactionReceipt receipt = null;
            if (!tx.StartsWith("0x"))
            {
                tx = "0x"+tx.ToLower();
            }

            receipt = EthUtils.RunSync(() => GetWeb3Client().Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tx));

            return receipt;
        }

        public List<EventLog<TransferEventDTO>> GetTransferEvents(string contract, BigInteger start, BigInteger end)
        {
            return GetTransferEvents(contract, new HexBigInteger(start), new HexBigInteger(end));
        }

        public List<EventLog<TransferEventDTO>> GetTransferEvents(string contract, HexBigInteger start, HexBigInteger end)
        {

            var transferEventHandler = GetWeb3Client().Eth.GetEvent<TransferEventDTO>(contract);

            var filter = transferEventHandler.CreateFilterInput(
                    fromBlock: new BlockParameter(start),
                    toBlock: new BlockParameter(end));

            var logs = EthUtils.RunSync(() => transferEventHandler.GetAllChanges(filter));

            return logs;

        }

        public EthTransferResult TryTransferAsset(string symbol, string toAddress, decimal amount, int decimals, out string result)
        {
            var nexus = NexusAPI.GetNexus();

            if (symbol.Equals("ETH", StringComparison.InvariantCultureIgnoreCase))
            {
                var bytes = nexus.GetOracleReader().Read<byte[]>(DateTime.Now, DomainExtensions.GetOracleFeeURL("ethereum"));
                var fees = new BigInteger(bytes);
                var gasPrice = Core.UnitConversion.ToDecimal(fees / Settings.Default.Oracle.EthGasLimit, 9);

                result = EthUtils.RunSync(() => GetWeb3Client().Eth.GetEtherTransferService()
                        .TransferEtherAsync(toAddress, amount, gasPrice, Settings.Default.Oracle.EthGasLimit));

                return EthTransferResult.Success;
            }
            else
            {
                var nativeAsset = false;
                if (symbol == DomainSettings.StakingTokenSymbol || symbol == DomainSettings.FuelTokenSymbol)
                {
                    nativeAsset = true;
                }

                var hash = nexus.GetTokenPlatformHash(symbol, "ethereum", nexus.RootStorage);

                if (hash.IsNull)
                {
                    result = null;
                    return EthTransferResult.Failure;
                }

                var contractAddress = hash.ToString().Substring(0, 40);
                if (string.IsNullOrEmpty(contractAddress))
                {
                    result = null;
                    return EthTransferResult.Failure;
                }

                string outTransactionHash = null;
                if (nativeAsset)
                {
                    var swapIn = new SwapInFunction()
                    {
                        Source = _account.Address,
                        Target = toAddress,
                        Amount = Nethereum.Util.UnitConversion.Convert.ToWei(amount, decimals)
                    };

                    var swapInHandler = GetWeb3Client().Eth.GetContractTransactionHandler<SwapInFunction>();

                    swapIn.Gas = Settings.Default.Oracle.EthGasLimit;
                    var bytes = nexus.GetOracleReader().Read<byte[]>(DateTime.Now, DomainExtensions.GetOracleFeeURL("ethereum"));
                    var fees = new BigInteger(bytes);
                    swapIn.GasPrice = BigInteger.Parse(fees.ToString()) / swapIn.Gas;

                    outTransactionHash = EthUtils.RunSync(() => swapInHandler
                            .SendRequestAsync(contractAddress, swapIn));
                }
                else
                {
                    var transferHandler = GetWeb3Client().Eth.GetContractTransactionHandler<TransferFunction>();
                    var transfer = new TransferFunction()
                    {
                        To = toAddress,
                        TokenAmount = Nethereum.Util.UnitConversion.Convert.ToWei(amount, decimals)
                    };
                    outTransactionHash = EthUtils.RunSync(() => transferHandler
                            .SendRequestAndWaitForReceiptAsync(contractAddress, transfer)).TransactionHash;
                    var a = transferHandler.Serialize();

                }

                result = outTransactionHash;

                return EthTransferResult.Success;
            }
        }

        public Web3 GetWeb3Client()
        {
            int idx = rnd.Next(web3Clients.Count);
            return web3Clients[idx];
        }

        public string GetURL()
        {
            int idx = rnd.Next(urls.Count);
            return "http://" + urls[idx];
        }
    }
}
