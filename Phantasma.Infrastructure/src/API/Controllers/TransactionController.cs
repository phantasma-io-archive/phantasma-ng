using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Shared.Types;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class TransactionController : BaseControllerV1
    {
        [APIInfo(typeof(TransactionResult), "Returns the information about a transaction requested by a block hash and transaction index.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("index transaction is invalid", "-1")]
        [HttpGet("GetTransactionByBlockHashAndIndex")]
        public TransactionResult GetTransactionByBlockHashAndIndex([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName, [APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash, [APIParameter("Index of transaction", "0")] int index)
        {
            var nexus = NexusAPI.GetNexus();

            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = chain.GetBlockByHash(hash);

                if (block == null)
                {
                    throw new APIException("unknown block hash");
                }

                if (index < 0 || index >= block.TransactionCount)
                {
                    throw new APIException("invalid transaction index");
                }

                var txHash = block.TransactionHashes.ElementAtOrDefault(index);

                if (txHash == Hash.Null)
                {
                    throw new APIException("unknown tx index");
                }

                return NexusAPI.FillTransaction(nexus.FindTransactionByHash(txHash));
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(AccountTransactionsResult), "Returns last X transactions of given address.", true, 3)]
        [APIFailCase("address is invalid", "543533")]
        [APIFailCase("page is invalid", "-1")]
        [APIFailCase("pageSize is invalid", "-1")]
        [HttpGet("GetAddressTransactions")]
        public PaginatedResult GetAddressTransactions([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Index of page to return", "5")] uint page = 1, [APIParameter("Number of items to return per page", "5")] uint pageSize = NexusAPI.PaginationMaxResults)
        {
            if (page < 1 || pageSize < 1)
            {
                throw new APIException("invalid page/pageSize");
            }

            if (pageSize > NexusAPI.PaginationMaxResults)
            {
                pageSize = NexusAPI.PaginationMaxResults;
            }

            if (Address.IsValidAddress(account))
            {
                var nexus = NexusAPI.GetNexus();

                var paginatedResult = new PaginatedResult();
                var address = Address.FromText(account);

                var chain = nexus.RootChain;
                // pagination
                var txHashes = chain.GetTransactionHashesForAddress(address);
                uint numberRecords = (uint)txHashes.Length;
                uint totalPages = (uint)Math.Ceiling(numberRecords / (double)pageSize);
                //

                var txs = txHashes.Select(x => chain.GetTransactionByHash(x))
                    .OrderByDescending(tx => nexus.FindBlockByTransaction(tx).Timestamp.Value)
                    .Skip((int)((page - 1) * pageSize))
                    .Take((int)pageSize);

                var result = new AccountTransactionsResult
                {
                    address = address.Text,
                    txs = txs.Select(NexusAPI.FillTransaction).ToArray()
                };

                paginatedResult.pageSize = pageSize;
                paginatedResult.totalPages = totalPages;
                paginatedResult.total = numberRecords;
                paginatedResult.page = page;

                paginatedResult.result = result;

                return paginatedResult;
            }
            else
            {
                throw new APIException("invalid address");
            }
        }

        [APIInfo(typeof(int), "Get number of transactions in a specific address and chain", false, 3)]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("chain is invalid", "-1")]
        [HttpGet("GetAddressTransactionCount")]
        public int GetAddressTransactionCount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Name or address of chain, optional", "apps")] string chainInput = "main")
        {
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            var address = Address.FromText(account);

            int count = 0;

            if (!string.IsNullOrEmpty(chainInput))
            {
                var chain = NexusAPI.FindChainByInput(chainInput);
                if (chain == null)
                {
                    throw new APIException("invalid chain");
                }

                var txHashes = chain.GetTransactionHashesForAddress(address);
                count = txHashes.Length;
            }
            else
            {
                var nexus = NexusAPI.GetNexus();

                var chains = nexus.GetChains(nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = nexus.GetChainByName(chainName);
                    var txHashes = chain.GetTransactionHashesForAddress(address);
                    count += txHashes.Length;
                }
            }

            return count;
        }

        [APIInfo(typeof(string), "Allows to broadcast a signed operation on the network, but it's required to build it manually. Does not wait on CheckTx or DeliverTx, returns instant", false, 0, true)]
        [APIFailCase("script is invalid", "")]
        [APIFailCase("failed to decoded transaction", "0000")]
        [HttpGet("SendRawTransactionAsync")]
        public string SendRawTransactionAsync([APIParameter("Serialized transaction bytes, in hexadecimal format", "0000000000")] string txData)
        {
            return "";
        }
        
        [APIInfo(typeof(string), "Allows to broadcast a signed operation on the network, but it's required to build it manually.", false, 0, true)]
        [APIFailCase("script is invalid", "")]
        [APIFailCase("failed to decoded transaction", "0000")]
        [HttpGet("SendRawTransaction")]
        public string SendRawTransaction([APIParameter("Serialized transaction bytes, in hexadecimal format", "0000000000")] string txData)
        {
            byte[] bytes;
            try
            {
                bytes = Base16.Decode(txData);
            }
            catch
            {
                return Hash.Null.ToString();
            }
            
            if (bytes.Length == 0)
            {
                return Hash.Null.ToString();
            }
            
            // TODO store deserialized tx to save some time later on
            var tx = Transaction.Unserialize(bytes);
            if (tx == null)
            {
                return Hash.Null.ToString();
            }

            var res = NexusAPI.TRPC.BroadcastTxSync(txData);
            return tx.Hash.ToString();
        }

        [APIInfo(typeof(ScriptResult), "Allows to invoke script based on network state, without state changes.", false, 5, true)]
        [APIFailCase("script is invalid", "")]
        [APIFailCase("failed to decoded script", "0000")]
        [HttpGet("InvokeRawScript")]
        public ScriptResult InvokeRawScript([APIParameter("Address or name of chain", "root")] string chainInput, [APIParameter("Serialized script bytes, in hexadecimal format", "0000000000")] string scriptData)
        {
            var chain = NexusAPI.FindChainByInput(chainInput);
            if (chain == null)
            {
                throw new APIException("invalid chain");
            }

            byte[] script;
            try
            {
                script = Base16.Decode(scriptData);
            }
            catch
            {
                throw new APIException("Failed to decode script");
            }

            if (script.Length == 0)
            {
                throw new APIException("Invalid transaction script");
            }

            //System.IO.File.AppendAllLines(@"c:\code\bug_vm.txt", new []{string.Join("\n", new VM.Disassembler(script).Instructions)});

            var nexus = NexusAPI.GetNexus();

            var changeSet = new StorageChangeSetContext(chain.Storage);
            var oracle = nexus.GetOracleReader();
            uint offset = 0;
            var vm = new RuntimeVM(-1, script, offset, chain, Address.Null, Timestamp.Now, null, changeSet, oracle, ChainTask.Null, true);

            string error = null;
            ExecutionState state = ExecutionState.Fault;
            try
            {
                state = vm.Execute();
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            if (error != null)
            {
                throw new APIException($"Execution failed: {error}");
            }

            var results = new Stack<string>();

            while (vm.Stack.Count > 0)
            {
                var result = vm.Stack.Pop();

                if (result.Type == VMType.Object)
                {
                    // NOTE currently supports simple arrays of C# objects. If something more complex in ncessary later, its good idea to rewrite this a recursive method
                    if (result.Data.GetType().IsArray)
                    {
                        var array1 = ((Array)result.Data);
                        var array2 = new VMObject[array1.Length];
                        for (int i=0; i<array1.Length; i++)
                        {
                            var obj = array1.GetValue(i);
                    
                            var vm_obj = VMObject.FromObject(obj);
                            vm_obj = VMObject.CastTo(result, VMType.Struct);
                    
                            array2[i] = vm_obj;
                        }
                    
                        result = VMObject.FromArray(array2);
                    }
                    else
                    {
                        result = VMObject.CastTo(result, VMType.Struct);
                    }
                }

                var resultBytes = Serialization.Serialize(result);
                results.Push(Base16.Encode(resultBytes));
            }

            var evts = vm.Events.Select(evt => new EventResult() { address = evt.Address.Text, kind = evt.Kind.ToString(), data = Base16.Encode(evt.Data) }).ToArray();

            var oracleReads = oracle.Entries.Select(x => new OracleResult()
            {
                url = x.URL,
                content = Base16.Encode((x.Content.GetType() == typeof(byte[]) ? x.Content as byte[] : Serialization.Serialize(x.Content)))
            }).ToArray();

            var resultArray = results.ToArray();
            return new ScriptResult { results = resultArray, result = resultArray.FirstOrDefault(), events = evts, oracles = oracleReads };
        }

        [APIInfo(typeof(TransactionResult), "Returns information about a transaction by hash.", false, -1, true)]
        [APIFailCase("hash is invalid", "43242342")]
        [HttpGet("GetTransaction")]
        public TransactionResult GetTransaction([APIParameter("Hash of transaction", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;
            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("Invalid hash");
            }

            var nexus = NexusAPI.GetNexus();

            var tx = nexus.FindTransactionByHash(hash);

            if (tx == null)
            {
                throw new APIException("Transaction not found");
            }

            return NexusAPI.FillTransaction(tx);
        }
    }
}
