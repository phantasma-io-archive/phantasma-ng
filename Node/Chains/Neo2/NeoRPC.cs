using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Neo;
using Neo.Wallets;
using Neo.Network.P2P.Payloads;
using Phantasma.Core;
using Phantasma.Shared.Utils;
using System.Text.Json;
using System.Linq;
//using Phantasma.Neo.Utils;
//using Phantasma.Storage;

namespace Phantasma.Spook.Chains
{
    public abstract class NeoRPC : NeoAPI
    {
        public readonly string neoscanUrl;

        public NeoRPC(string neoscanURL)
        {
            this.neoscanUrl = neoscanURL;
        }

        public static NeoRPC ForMainNet(NEONodesKind kind = NEONodesKind.COZ)
        {
            return new RemoteRPCNode(10332, "http://neoscan.io", kind);
        }

        public static NeoRPC ForTestNet()
        {
            return new RemoteRPCNode(20332, "https://neoscan-testnet.io", NEONodesKind.NEO_ORG);
        }

        public static NeoRPC ForPrivateNet()
        {
            return new LocalRPCNode(30333, "http://localhost:4000");
        }

        #region RPC API
        public string rpcEndpoint { get; set; }
        private static object rpcEndpointUpdateLocker = new object();

        protected abstract string GetRPCEndpoint();

        private void LogData(DataNode node, int ident = 0)
        {
            var tabs = new string('\t', ident);
            Logger($"{tabs}{node}");
            foreach (DataNode child in node.Children)
                LogData(child, ident + 1);
        }

        public JsonDocument QueryRPC(string method, object[] _params, int id = 1, bool numeric = false, string node = null
                , Action<string> chosenRpc = null)
        {
            var paramData = new List<object>();
            foreach (var entry in _params)
            {
                if (numeric)
                {
                    paramData.Add((int)entry);
                }
                else if (entry.GetType() == typeof(BigInteger))
                { 
                    /*
                     * TODO sufficient for neo2 but needs a better solution in the future.
                     * Could fail if entry > maxInt.
                     */
                    paramData.Add((int)(BigInteger)entry);
                }
                else
                {
                    paramData.Add(entry);
                }
            }

            //Logger("QueryRPC: " + method);
            //LogData(jsonRpcData);

            int retryCount = 0;
            do
            { 
                // Using local var to avoid it being nullified by another thread right before RequestUtils.Request() call.
                string currentRpcEndpoint;
                if (!string.IsNullOrEmpty(node))
                {
                    currentRpcEndpoint = node;
                }
                else
                {
                    lock (rpcEndpointUpdateLocker)
                    {
                        if (rpcEndpoint == null)
                        {
                            rpcEndpoint = GetRPCEndpoint();
                            Logger("Update RPC Endpoint: " + rpcEndpoint);
                        }
                        currentRpcEndpoint = rpcEndpoint;
                    }
                }

                if (chosenRpc != null)
                {
                    chosenRpc.Invoke(currentRpcEndpoint);
                }

                //Logger($"NeoRPC: QueryRPC({currentRpcEndpoint}): data: " + jsonRpcData != null ? JSONWriter.WriteToString(jsonRpcData) : "{}");
                var response = RequestUtils.RPCRequest(currentRpcEndpoint, method, out var _, 0, 1, paramData.ToArray());

                if (response != null)
                {
                    if (response.RootElement.TryGetProperty("result", out var resultProperty))
                    {
                        LastError = null;
                        return response;
                    }

                    if (response.RootElement.TryGetProperty("error", out var errorProperty))
                    {
                        LastError = errorProperty.GetProperty("message").GetString();
                    }
                    else
                    {
                        LastError = "Unknown RPC error";
                    }
                }
                else
                {
                    LastError = $"NeoRPC: QueryRPC({currentRpcEndpoint}): Connection failure";
                }

                Logger("RPC Error: " + LastError);
                rpcEndpoint = null;
                retryCount++;
                Thread.Sleep(1000);

            } while (retryCount < 5);

            return null;
        }
        #endregion

        public override bool HasPlugin(string pluginName)
        {
            var response = QueryRPC("listplugins", new object[]{});
            var resultNode = response.RootElement.GetProperty("result");

            foreach (var entry in resultNode.EnumerateArray())
            {
                foreach (var en in entry.EnumerateArray())
                {
                    if(en.TryGetProperty("name", out var nameProperty))
                    {
                        if (string.Equals(nameProperty.GetString(), pluginName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public override bool CheckMempool(string node, string txHash)
        {
            var mempool = GetMempool(node, true);

            if (!txHash.StartsWith("0x"))
            {
                txHash = "0x"+ txHash;
            }

            if (mempool.Contains(txHash))
            {
                return true;
            }

            return false;
        }

        public override List<string> GetMempool(string node, bool _unverified)
        {
            var response = QueryRPC("getrawmempool", new object[] { _unverified }, 1, false, node);
            var result = new List<string>();

            var resultNode = response.RootElement.GetProperty("result");

            if (_unverified)
            {
                var verified = resultNode.GetProperty("verified");
                var unverified = resultNode.GetProperty("unverified");

                foreach (var entry in verified.EnumerateArray())
                {
                    result.Add(entry.GetString());
                }

                foreach (var entry in unverified.EnumerateArray())
                {
                    result.Add(entry.GetString());
                }
            }
            else
            {
                foreach (var entry in resultNode.EnumerateArray())
                {
                    result.Add(entry.GetString());
                }
            }

            return result;

        }

        public override string GetNep5Transfers(UInt160 scriptHash, DateTime timestamp)
        {
            if (!HasPlugin("RpcNep5Tracker"))
            {
                return null;
            }

            var unixTimestamp = (timestamp.ToUniversalTime()
                    - (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))).TotalSeconds;

            var response = QueryRPC("getnep5transfers", new object[] { scriptHash.ToAddress(), unixTimestamp });

            return response.ToJsonString();
        }

        public override string GetUnspents(UInt160 scriptHash)
        {
            if (!HasPlugin("RpcSystemAssetTrackerPlugin"))
            {
                return null;
            }

            var response = QueryRPC("getunspents", new object[] { scriptHash.ToAddress() });

            return response.ToJsonString();
        }

        public override Dictionary<string, decimal> GetAssetBalancesOf(UInt160 scriptHash)
        {
            var response = QueryRPC("getaccountstate", new object[] { scriptHash.ToAddress() });
            var result = new Dictionary<string, decimal>();

            var resultNode = response.RootElement.GetProperty("result");
            var balances = resultNode.GetProperty("balances");

            foreach (var entry in balances.EnumerateArray())
            {
                var assetID = entry.GetProperty("asset").GetString();
                var amount = entry.GetProperty("value").GetDecimal();

                var symbol = SymbolFromAssetID(assetID);

                result[symbol] = amount;
            }

            return result;
        }

        public override byte[] GetStorage(string scriptHash, byte[] key)
        {
            var response = QueryRPC("getstorage", new object[] { key.ByteToHex() });
            var result = response.RootElement.GetProperty("result").GetString();
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }
            return result.HexToBytes();
        }

        // Note: This current implementation requires NeoScan running at port 4000
        public override Dictionary<string, List<UnspentEntry>> GetUnspent(UInt160 hash)
        {
            var url = this.neoscanUrl +"/api/main_net/v1/get_balance/" + hash.ToAddress();
            var response = RequestUtils.Request<JsonDocument>(RequestType.GET, url, out var _);

            var unspents = new Dictionary<string, List<UnspentEntry>>();

            var root = response.RootElement.GetProperty("balance");

            foreach (var child in root.EnumerateArray())
            {
                var symbol = child.GetProperty("asset").GetString();

                List<UnspentEntry> list = new List<UnspentEntry>();
                unspents[symbol] = list;

                var unspentNode = child.GetProperty("unspent");
                foreach (var entry in unspentNode.EnumerateArray())
                {
                    var txid = entry.GetProperty("txid").GetString();
                    var val = entry.GetProperty("value").GetDecimal();
                    var temp = new UnspentEntry() { hash = new UInt256(NeoUtils.ReverseHex(txid).HexToBytes()), value = val, index = (ushort)entry.GetProperty("n").GetUInt32() };
                    list.Add(temp);
                }
            }

            return unspents;
        }

        // Note: This current implementation requires NeoScan running at port 4000
        public override List<UnspentEntry> GetClaimable(UInt160 hash, out decimal amount)
        {
            var url = this.neoscanUrl + "/api/main_net/v1/get_claimable/" + hash.ToAddress();
            var response = RequestUtils.Request<JsonDocument>(RequestType.GET, url, out var _);

            var result = new List<UnspentEntry>();

            amount = response.RootElement.GetProperty("unclaimed").GetDecimal();

            var root = response.RootElement.GetProperty("claimable");

            foreach (var child in root.EnumerateArray())
            {
                var txid = child.GetProperty("txid").GetString();
                var index = (ushort)child.GetProperty("n").GetUInt32();
                var value = child.GetProperty("unclaimed").GetDecimal();

                result.Add(new UnspentEntry() { hash = new UInt256(NeoUtils.ReverseHex(txid).HexToBytes()), index = index, value = value });
            }

            return result;
        }

        public bool SendRawTransaction(string hexTx, out string usedRpc)
        {
            string chosenRpc = null;
            var response = QueryRPC("sendrawtransaction", new object[] { hexTx }, 1, false, null, x => chosenRpc = x);
            usedRpc = chosenRpc;
            if (response == null)
            {
                throw new Exception($"SendRawTransaction({hexTx}): Connection failure");
            }

            try
            {
                var temp = response.RootElement.GetProperty("result");

                bool result;

                if (temp.TryGetProperty("succeed", out var succeedProperty))
                {
                    result = succeedProperty.GetBoolean();
                }
                else
                {
                    try
                    {
                        result = temp.GetBoolean();
                    }
                    catch
                    {
                        result = false;
                    }
                }

                return result;
            }
            catch
            {
                return false;
            }
        }

        protected override bool SendTransaction(Transaction tx, out string usedRpc)
        {
            //var rawTx = tx.Serialize(true);
            var rawTx = tx.Serialize();
            var hexTx = rawTx.ByteToHex();

            return SendRawTransaction(hexTx, out usedRpc);
        }

        public override InvokeResult InvokeScript(byte[] script)
        {
            var invoke = new InvokeResult();
            invoke.state = VMState.NONE;

            var response = QueryRPC("invokescript", new object[] { script.ByteToHex()});

            if (response != null)
            {
                if (response.RootElement.TryGetProperty("result", out var resultProperty))
                {
                    var stack = resultProperty.GetProperty("stack");
                    invoke.result = ParseStack(stack);

                    invoke.gasSpent = resultProperty.GetProperty("gas_consumed").GetDecimal();
                    var temp = resultProperty.GetProperty("state").GetString();

                    if (temp.Contains("FAULT"))
                    {
                        invoke.state = VMState.FAULT;
                    }
                    else
                    if (temp.Contains("HALT"))
                    {
                        invoke.state = VMState.HALT;
                    }
                    else
                    {
                        invoke.state = VMState.NONE;
                    }
                }
            }

            return invoke;
        }

        public override string GetTransactionHeight(UInt256 hash)
        {
            string chosenRpc = null;
            var response = QueryRPC("gettransactionheight", new object[] { hash.ToString() }, 1, false
                    , null, x => chosenRpc = x);

            if (response.RootElement.TryGetProperty("result", out var resultProperty))
            {
                return resultProperty.GetString();
            }
            else
            {
                return null;
            }
        }

        public override Dictionary<string, BigInteger> GetSwapBlocks(string hash, string address, string height = null)
        {
            var objects = new List<object>() {hash, address};
            if (!string.IsNullOrEmpty(height))
            {
                objects.Add(BigInteger.Parse(height));
            }

            var response = QueryRPC("getblockids", objects.ToArray());
            if (response.RootElement.TryGetProperty("result", out var resultProperty))
            {
                var blockIds = new Dictionary<string, BigInteger>();

                var blocks = resultProperty.EnumerateArray();
                //LogData(blocks);
                for (var i = 0; i < blocks.Count(); i++)
                {
                    var blockHash = blocks.ElementAt(i).GetProperty("block_hash").GetString();
                    if (blockHash.StartsWith("0x"))
                    {
                        blockHash = Hash.Parse(blockHash.Substring(2)).ToString();
                    }

                    blockIds.Add(blockHash, new BigInteger(blocks.ElementAt(i).GetProperty("block_index").GetInt32()));
                }

                return blockIds;
            }
            else
            {
                return new Dictionary<string, BigInteger>();
            }
        }

        public override ApplicationLog[] GetApplicationLog(UInt256 hash)
        {
            if (!HasPlugin("ApplicationLogs"))
            {
                return null;
            }

            var response = QueryRPC("getapplicationlog", new object[] { hash.ToString() });
            if (response.RootElement.TryGetProperty("result", out var resultProperty))
            {
                //var json = LunarLabs.Parser.JSON.JSONReader.ReadFromString(response);
                List<ApplicationLog> appLogList = new List<ApplicationLog>();

                var executions = resultProperty.GetProperty("executions").EnumerateArray();
                //LogData(executions);
                for (var i = 0; i < executions.Count(); i++)
                {
                    VMState vmstate;
                    if (Enum.TryParse(executions.ElementAt(i).GetProperty("vmstate").GetString(), out vmstate))
                    {
                        //LogData(executions[i]["notifications"][0]["state"]["value"]);
                        var notifications = executions.ElementAt(i).GetProperty("notifications").EnumerateArray();
                        for (var j = 0; j < notifications.Count(); j++)
                        {
                            var states = notifications.ElementAt(i).GetProperty("state").GetProperty("value").EnumerateArray();
                            string txevent = "";
                            UInt160 source = UInt160.Zero;
                            UInt160 target = UInt160.Zero;
                            BigInteger amount = 0;
                            var contract = notifications.ElementAt(i).GetProperty("contract").GetString(); 

                            if(states.ElementAt(0).GetProperty("type").GetString() == "ByteArray")
                                txevent = states.ElementAt(0).GetProperty("value").GetString();

                            if(states.ElementAt(1).GetProperty("type").GetString() == "ByteArray" 
                                    && !string.IsNullOrEmpty(states.ElementAt(1).GetProperty("value").GetString()))
                                source = UInt160.Parse(states.ElementAt(1).GetProperty("value").GetString());

                            if(states.ElementAt(2).GetProperty("type").GetString() == "ByteArray" 
                                    && !string.IsNullOrEmpty(states.ElementAt(2).GetProperty("value").GetString()))
                                target = UInt160.Parse(states.ElementAt(2).GetProperty("value").GetString());

                            if (states.ElementAt(3).GetProperty("type").GetString() == "ByteArray")
                            {
                                amount = new BigInteger(states.ElementAt(3).GetProperty("value").GetString().HexToBytes()); // needs to be Phantasma.Numerics.BigInteger for now.
                            }
                            appLogList.Add(new ApplicationLog(vmstate, contract, txevent, source, target, amount));
                        }
                    }
                }

                return appLogList.ToArray();
            }
            else
            {
                return null;
            }
        }

        public override Transaction GetTransaction(UInt256 hash)
        {
            var response = QueryRPC("getrawtransaction", new object[] { hash.ToString() });
            if (response.RootElement.TryGetProperty("result", out var resultProperty))
            {
                var bytes = resultProperty.GetString().HexToBytes();
                //return Transaction.Unserialize(bytes);
                return Transaction.DeserializeFrom(bytes);
            }
            else
            {
                return null;
            }
        }

        public override BigInteger GetBlockHeight()
        {
            var response = QueryRPC("getblockcount", new object[] { });
            return response.RootElement.GetProperty("result").GetUInt32();
        }

        public override List<Block> GetBlockRange(BigInteger start, BigInteger end)
        {
            var taskList = new List<Task<JsonDocument>>();
            var blockList = new List<Block>();

            for (var i = start; i < end; i++)
            {
                var height = i;
                object[] heightData = new object[] { (int)height };

                taskList.Add(
                        new Task<JsonDocument>(() => 
                        {
                            return QueryRPC("getblock", heightData, 1, true);
                        })
                );
            }

            foreach (var task in taskList)
            {
                task.Start();
            }

            Task.WaitAll(taskList.ToArray());

            foreach (var task in taskList)
            {
                var response = task.Result;

                if (response.RootElement.TryGetProperty("result", out var resultProperty))
                {
                    var bytes = resultProperty.GetString().HexToBytes();

                    using (var stream = new MemoryStream(bytes))
                    {
                        using (var reader = new BinaryReader(stream))
                        {
                            var block = new Block();
                            block.Deserialize(reader);
                            blockList.Add(block);
                        }
                    }
                }
                else
                {
                    return null;
                }
            }

            return blockList;
        }

        public override Block GetBlock(BigInteger height)
        {
            object[] heightData = new object[] { (int)height };
            var response = QueryRPC("getblock", heightData, 1, true);
            if (response.RootElement.TryGetProperty("result", out var resultProperty))
            {
                var result = resultProperty.GetString();

                var bytes = result.HexToBytes();

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {

                        var block = new Block();
                        block.Deserialize(reader);
                        return block;
                    }
                }
            }
            else
            {
                return null;
            }
        }

        public override Block GetBlock(UInt256 hash)
        {
            var response = QueryRPC("getblock", new object[] { hash.ToString() });
            if (response.RootElement.TryGetProperty("result", out var resultProperty))
            {
                var result = resultProperty.GetString();

                var bytes = result.HexToBytes();

                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var block = new Block();
                        block.Deserialize(reader);
                        return block;
                    }
                }
            }
            else
            {
                return null;
            }
        }
    }

    public class LocalRPCNode : NeoRPC
    {
        private int port;

        public LocalRPCNode(int port, string neoscanURL) : base(neoscanURL)
        {
            this.port = port;
        }

        protected override string GetRPCEndpoint()
        {
            return $"http://localhost:{port}";
        }
    }

    public enum NEONodesKind
    {
        NEO_ORG,
        COZ,
        TRAVALA
    }

    public class RemoteRPCNode : NeoRPC
    {
        private int rpcIndex = 0;

        private string[] nodes;

        public RemoteRPCNode(string neoscanURL, params string[] nodes) : base(neoscanURL)
        {
            this.nodes = nodes;
        }

        public RemoteRPCNode(int port, string neoscanURL, NEONodesKind kind) : base(neoscanURL)
        {
            switch (kind)
            {
                case NEONodesKind.NEO_ORG:
                    {
                        nodes = new string[5];
                        for (int i = 0; i < nodes.Length; i++)
                        {
                            nodes[i] = $"http://seed{i}.neo.org:{port}";
                        }
                        break;
                    }

                case NEONodesKind.COZ:
                    {
                        if (port == 10331)
                        {
                            port = 443;
                        }

                        nodes = new string[5];
                        for (int i = 0; i < nodes.Length; i++)
                        {
                            nodes[i] = $"http://seed{i}.cityofzion.io:{port}";
                        }
                        break;
                    }

                case NEONodesKind.TRAVALA:
                    {
                        nodes = new string[5];
                        for (int i = 0; i < nodes.Length; i++)
                        {
                            nodes[i] = $"http://seed{i}.travala.com:{port}";
                        }
                        break;
                    }
            }
        }

        protected override string GetRPCEndpoint()
        {
            rpcIndex++;
            if (rpcIndex >= nodes.Length)
            {
                rpcIndex = 0;
            }

            return nodes[rpcIndex];
        }
    }
}
