using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Phantasma.Business;
using Phantasma.Shared.Types;
using Phantasma.Core;
using Tendermint.Abci;
using Types;
using Serilog;
using Tendermint.RPC;
using System.Text.Json;
using System.Linq;

namespace Phantasma.Node;
public class ABCIConnector : ABCIApplication.ABCIApplicationBase
{
    private Nexus _nexus;
    private PhantasmaKeys _owner;
    private NodeRpcClient _rpc;
    private SortedDictionary<int, Transaction>_systemTxs = new SortedDictionary<int, Transaction>();
    private List<Transaction> _broadcastedTxs = new List<Transaction>();

    // TODO add logger
    public ABCIConnector() { Log.Information("ABCI Connector initialized"); }

    public void SetNodeInfo(Nexus nexus, string tendermintEndpoint, PhantasmaKeys keys)
    {
        _owner = keys;
        _nexus = nexus;
        _rpc = new NodeRpcClient(tendermintEndpoint);
        _nexus.RootChain.ValidatorKeys = _owner;
    }

    public override Task<ResponseBeginBlock> BeginBlock(RequestBeginBlock request, ServerCallContext context)
    {
        var response = new ResponseBeginBlock();
        try
        {
            var proposerAddress = Base16.Encode(request.Header.ProposerAddress.ToByteArray());
            Log.Information($"proposer {proposerAddress} current node {this._owner.Address.TendermintAddress}");
            if (proposerAddress.Equals(this._owner.Address.TendermintAddress))
            {
                foreach (var tx in _systemTxs.OrderBy(x => x.Key))
                {
                    var txString = Base16.Encode(tx.Value.ToByteArray(true));
                    Log.Information($"Broadcast tx {tx}");
                    _rpc.BroadcastTxSync(txString);
                    _broadcastedTxs.Add(tx.Value);
                }
                _systemTxs.Clear();
            }
            var kp = PhantasmaKeys.Generate();
            Console.WriteLine("TAddress: " + kp.Address.TendermintAddress);
            Console.WriteLine("TPubBase64: " + Convert.ToBase64String(kp.PublicKey));
            Console.WriteLine("TPrvBase64: " + Convert.ToBase64String(kp.PrivateKey.Concat(kp.PublicKey).ToArray()));
            Console.WriteLine("TWIF: " + kp.ToWIF());
            Console.WriteLine("check " + PhantasmaKeys.FromWIF(kp.ToWIF()));

            //var strii = request.Header.ProposerAddress.ToStringUtf8();
            ////var address = Address.FromBytes(bytes);

            //Console.WriteLine("validator: " + this._owner.Address); 
            //Console.WriteLine("tvalidator: " + this._owner.Address.TendermintAddress); 
            //Console.WriteLine("block val base16: " + base16); 
            // private base64 w0mmOZ+uL/c210eB/Iq26ZlIkgVDqQUPYPOo8cxjMNkzCafWCkiZiR9oYShDmi450bUybwcBiDggq2mCZvhnKw==
            Console.WriteLine("node0: " + new PhantasmaKeys(Convert.FromBase64String("9hXo3RT1MmwO+BySGvMlZoOX6mpEZ3fFHfIcG5OgSNKhZ9NyIfVKX8Tc5XswVxNsCfBSrI8TKKot3K299WrZBg==")).ToWIF());
            Console.WriteLine("node1: " + new PhantasmaKeys(Convert.FromBase64String("1gA+fxcnKN8KUEUZ2DT1E1cDDs9TszA5g+E+DVZqnPQfwoCENyyKI0bTg9NOSGip9+kbJlAvp4c8PBO3dbtKUQ==")).ToWIF());
            Console.WriteLine("node2: " + new PhantasmaKeys(Convert.FromBase64String("0bxNN0SgWCdcAfP9kJuZsA4ZckobDnrXUpUF2t8EomtdqNI4zpAiSjfRH0V9iol3pBCz9XVFlZq9e3Ca+zdOCw==")).ToWIF());
            Console.WriteLine("node3: " + new PhantasmaKeys(Convert.FromBase64String("/6avjlC7uF1dTGh/kAEG6yVSvGTgMfSLU4BLhAIKFuqlvNEoV33bjSiK0WAa+cghWYuAMBHia113WWYimJvIsQ==")).ToWIF());

            var node3 = Convert.FromBase64String("B6dzjjiNh6I0aa2gulbD1qv8I/q+IhNGP82yAuzqvZZCSldCbRU6FgYpWg0NQ393Ms9hGDdX+K+/HWjMsvwgiA==");
            var node3Key = new PhantasmaKeys(node3);
            //var a = Convert.FromBase64String("+ezHfdCg==");
            //Console.WriteLine("test: " + xx.Address.TendermintAddress); 
            //Console.WriteLine("test2: " + xx.ToWIF()); 
            Console.WriteLine("node3: " + node3Key.ToWIF()); 

            IEnumerable<Transaction> systemTransactions;
            systemTransactions = _nexus.RootChain.BeginBlock(request.Header); 

            if (proposerAddress.Equals(this._owner.Address.TendermintAddress))
            {
                var idx = 0;
                foreach (var tx in systemTransactions)
                {
                    Log.Information("Broadcasting system transaction {}", tx);
                    _systemTxs.Add(idx, tx);
                    var txString = Base16.Encode(tx.ToByteArray(true));
                    Task.Factory.StartNew(() => _rpc.BroadcastTxSync(txString));
                    idx++;
                }
            }
        }
        catch (Exception e)
        {
            Log.Information(e.ToString());
        }
        
        return Task.FromResult(response);
    }
    
    public override Task<ResponseCheckTx> CheckTx(RequestCheckTx request, ServerCallContext context)
    {
        // TODO checktx 
        try
        {
            Log.Debug("CheckTx called");
            (CodeType code, string message) = _nexus.RootChain.CheckTx(request.Tx);

            var response = new ResponseCheckTx();
            response.Code = 0;
            if (code == CodeType.Ok)    
            {
                return Task.FromResult(ResponseHelper.Check.Ok());
            }
        }
        catch (Exception e)
        {

            Log.Information("exception: " + e);
        }
        return Task.FromResult(ResponseHelper.Check.Ok());
    }
    
    public override Task<ResponseDeliverTx> DeliverTx(RequestDeliverTx request, ServerCallContext context)
    {
        Log.Information("DeliverTx called");
        var result = _nexus.RootChain.DeliverTx(request.Tx);

        var bytes = Serialization.Serialize(result.Result);
        var response = new ResponseDeliverTx()
        {
            Code = result.Code,
            Codespace = result.Codespace,
            Data = ByteString.CopyFrom(bytes),
            //response.Log = ???
            //response.info = ???
            //response.GasWanted = ???
            //response.GasUsed = ???
            //response.Events = TODO
        };

        var toDelete = new List<Transaction>();
        foreach (var tx in _broadcastedTxs)
        {
            if (tx.Hash == result.Hash)
            {
                toDelete.Add(tx);
            }
        }

        foreach (var tx in toDelete)
        {
            Console.WriteLine("delete " + tx);
            _broadcastedTxs.Remove(tx);
        }

        return Task.FromResult(response);
    }

    public override Task<ResponseEndBlock> EndBlock(RequestEndBlock request, ServerCallContext context)
    {
        var response = new ResponseEndBlock();
        try
        {
            var result = _nexus.RootChain.EndBlock();

            response.ValidatorUpdates.AddRange(result);

            // TODO
            //response.ConsensusParamUpdates = ???
            //response.Events = ???

            return Task.FromResult(response);

        }
        catch (Exception e)
        {
            Log.Information(e.ToString());
        }
        return Task.FromResult(response);
    }
    
    public override Task<ResponseCommit> Commit(RequestCommit request, ServerCallContext context)
    {
        var data = _nexus.RootChain.Commit();
        var response = new ResponseCommit();
        //response.Data = ByteString.CopyFrom(data); // this would change the app hash, we don't want that
        return Task.FromResult(response);
    }


    public override Task<ResponseEcho> Echo(RequestEcho request, ServerCallContext context)
    {
        var echo = new ResponseEcho();
        echo.Message = request.Message;
        return Task.FromResult(echo);
    }

    public override Task<ResponseFlush> Flush(RequestFlush request, ServerCallContext context)
    {
        Log.Information("RequestFlush has been called.");
        return Task.FromResult(new ResponseFlush());
    }

    public override Task<ResponseInfo> Info(RequestInfo request, ServerCallContext context)
    {
        Hash lastBlockHash;
        Block lastBlock = null;
        ResponseInfo response = null;
        try 
        {
            lastBlockHash = _nexus.RootChain.GetLastBlockHash();
            lastBlock = _nexus.RootChain.GetBlockByHash(lastBlockHash);

            response = new ResponseInfo() {
                AppVersion = _nexus.GetProtocolVersion(_nexus.RootStorage),
                LastBlockAppHash = ByteString.CopyFrom(_nexus.GetProtocolVersion(_nexus.RootStorage).ToString(), Encoding.UTF8) ,

                LastBlockHeight = (lastBlock != null) ? (long)lastBlock.Height : 0,
                Version = "0.0.1",
            };
        }
        catch (Exception e)
        {
            Log.Information("Error getting info " + e);
        }

        return Task.FromResult(response);
    }

    public override Task<ResponseInitChain> InitChain(RequestInitChain request, ServerCallContext context)
    {
        var response = new ResponseInitChain();
        var timestamp = new Timestamp((uint) request.Time.Seconds);

        try
        {
            Dictionary<int, Transaction> systemTransactions;
            systemTransactions = _nexus.CreateGenesisBlock(timestamp, 0, this._owner);

            var idx = 0;
            foreach (var tx in systemTransactions.OrderByDescending(x => x.Key))
            {
                Log.Information($"Preparing tx {tx.Value} for broadcast");
                _systemTxs.Add(tx.Key, tx.Value);
                idx++;
            }
        }
        catch (Exception e)
        {
            Log.Information(e.ToString());
        }

        var appHash = Encoding.UTF8.GetBytes("A Phantasma was born...");
        response.AppHash = ByteString.CopyFrom(appHash);
        return Task.FromResult( response );
    }

    public override Task<ResponseQuery> Query(RequestQuery request, ServerCallContext context)
    {
        return Task.FromResult( new ResponseQuery());
    }

    public override Task<ResponseListSnapshots> ListSnapshots(RequestListSnapshots request, ServerCallContext context)
    {
        return Task.FromResult( new ResponseListSnapshots());
    }

    public override Task<ResponseOfferSnapshot> OfferSnapshot(RequestOfferSnapshot request, ServerCallContext context)
    {
        return Task.FromResult( new ResponseOfferSnapshot());
    }

    public override Task<ResponseLoadSnapshotChunk> LoadSnapshotChunk(RequestLoadSnapshotChunk request, ServerCallContext context)
    {
        return Task.FromResult( new ResponseLoadSnapshotChunk());
    }

    public override Task<ResponseApplySnapshotChunk> ApplySnapshotChunk(RequestApplySnapshotChunk request, ServerCallContext context)
    {
        return Task.FromResult( new ResponseApplySnapshotChunk());
    }
}
