using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Serilog;
using Tendermint;
using Tendermint.Abci;
using Tendermint.Extensions;
using Tendermint.RPC;
using Chain = Phantasma.Business.Blockchain.Chain;

namespace Phantasma.Node;
public class ABCIConnector : ABCIApplication.ABCIApplicationBase
{
    private Nexus _nexus;
    private PhantasmaKeys _owner;
    private NodeRpcClient _rpc;
    private IEnumerable<Address> _initialValidators;
    private List<Transaction> _pendingTxs = new List<Transaction>();
    private BigInteger _minimumFee;
    private Timestamp currentBlockTime;
    private int _delayRequests = 1000;

    // TODO add logger
    public ABCIConnector(IEnumerable<Address> initialValidators, BigInteger minimumFee)
    {
        _minimumFee = minimumFee;
        _initialValidators = initialValidators;
        Log.Information("ABCI Connector initialized");
    }

    public void SetNodeInfo(Nexus nexus, string tendermintEndpoint, PhantasmaKeys keys)
    {
        _owner = keys;
        _nexus = nexus;
        _rpc = new NodeRpcClient(tendermintEndpoint);
        _nexus.RootChain.ValidatorKeys = _owner;
        Log.Information("ABCI Connector - Set node Info");

    }

    public bool IsTransactionPending(Hash hash)
    {
        return _pendingTxs.Any(tx => tx.Hash == hash);
    }

    public override Task<ResponseBeginBlock> BeginBlock(RequestBeginBlock request, ServerCallContext context)
    {
        Timestamp time = new Timestamp((uint) request.Header.Time.Seconds);
        currentBlockTime = time;
        Log.Information("Begin block {Height} at {time}", request.Header.Height, time);

        var response = new ResponseBeginBlock();
        try
        {
            var proposerAddress = Base16.Encode(request.Header.ProposerAddress.ToByteArray());
            Log.Information("proposer {ProposerAddress} current node {CurrentAddress}", proposerAddress, this._owner.Address.TendermintAddress);
            if (proposerAddress.Equals(this._owner.Address.TendermintAddress))
            {
                foreach (var tx in _pendingTxs)
                {
                    var txString = Base16.Encode(tx.ToByteArray(true));
                    Log.Information("Broadcast tx {Transaction}", tx);
                    while (true)
                    {
                        try
                        {
                            _rpc.BroadcastTxSync(txString);
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    Log.Information("Broadcast tx {Transaction} done", tx);
                }
            }

            var chain = _nexus.RootChain as Chain;

            IEnumerable<Transaction> systemTransactions;
            if (chain.CurrentBlock != null)
            {
                AttemptRequestBlock(chain);
            }
            systemTransactions = chain.BeginBlock(proposerAddress, request.Header.Height, _minimumFee, time, this._initialValidators); 
        }
        catch (Exception e)
        {
            Log.Information(e.ToString());
        }
        
        return Task.FromResult(response);
    }
    
    public override Task<ResponseCheckTx> CheckTx(RequestCheckTx request, ServerCallContext context)
    {
        Log.Information($"ABCI Connector - Check TX");

        try
        {
            if (request.Type == CheckTxType.New)
            {
                var chain = _nexus.RootChain as Chain;

                var txString = request.Tx.ToStringUtf8();
                var tx = Transaction.Unserialize(Base16.Decode(txString));

                (CodeType code, string message) = chain.CheckTx(tx, currentBlockTime);

                var response = new ResponseCheckTx();
                response.Code = 0;
                if (code == CodeType.Ok)    
                {
                    return Task.FromResult(ResponseHelper.Check.Ok());
                }

                return Task.FromResult(ResponseHelper.Check.Create(code, message));
            }
        }
        catch (Exception e)
        {
            Log.Information("CheckTx failed: {Exception}", e);
        }

        return Task.FromResult(ResponseHelper.Check.Create(CodeType.Error, "Generic Error"));
    }
    
    public override Task<ResponseDeliverTx> DeliverTx(RequestDeliverTx request, ServerCallContext context)
    {
        Log.Information($"ABCI Connector - Deliver Tx");

        var chain = _nexus.RootChain as Chain;

        var txString = request.Tx.ToStringUtf8();
        var newTx = Transaction.Unserialize(Base16.Decode(txString));

        var result = chain.DeliverTx(newTx);

        var bytes = Serialization.Serialize(result.Result);

        var response = new ResponseDeliverTx()
        {
            Code = result.Code,
            // Codespace cannot be null!
            Codespace = result.Codespace,
            Data = ByteString.CopyFrom(bytes),
        };

        // Fix for null transaction that crashed the chain to many times!
        if ( result.Events != null) // Yes it was just a null check that was missing!
            if (result.Events.Count() > 0)
            {
                var newEvents = new List<Tendermint.Abci.Event>();
                foreach (var evt in result.Events)
                {
                    var newEvent = new Tendermint.Abci.Event();
                    var attributes = new EventAttribute[]
                    {
                        // Value cannot be null!
                        new EventAttribute() { Key = "address", Value = evt.Address.ToString() },
                        new EventAttribute() { Key = "contract", Value = evt.Contract },
                        new EventAttribute() { Key = "data", Value = Base16.Encode(evt.Data) },
                    };

                    newEvent.Type = evt.Kind.ToString();
                    newEvent.Attributes.AddRange(attributes);

                    newEvents.Add(newEvent);
                }
                response.Events.AddRange(newEvents);
            }

        // check if a system tx was executed, if yes, remove it
        for (var i = 0; i < _pendingTxs.Count; i++)
        {
            var tx = _pendingTxs[i];
            if (tx.Hash == result.Hash)
            {
                Log.Information($"Transaction {tx.Hash} has been executed, remove now");
                _pendingTxs.Remove(tx);
            }
        }

        return Task.FromResult(response);
    }

    public override Task<ResponseEndBlock> EndBlock(RequestEndBlock request, ServerCallContext context)
    {
        Log.Information("End block {Height}", request.Height);
        var response = new ResponseEndBlock();
        try
        {
            var chain = _nexus.RootChain as Chain;
            var result = chain.EndBlock<ValidatorUpdate>();

            response.ValidatorUpdates.AddRange(result);

            // TODO
            //response.ConsensusParamUpdates = ???
            //response.Events = ???


            if (chain.Height == 1 && _nexus.Name != "mainnet")
            {
                Console.WriteLine("NODE ADDRESS: " + _owner.Address);
                Console.WriteLine("NODE WIF: " + _owner.ToWIF());
            }

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
        Log.Information($"ABCI Connector - Commit");

        var chain = _nexus.RootChain as Chain;
        // Is signed by me and I am the proposer
        Log.Information("Block {Height} is signed by {Address}", chain.Height, chain.CurrentBlock.Validator);
        if (chain.CurrentBlock.Validator == chain.ValidatorAddress)
        {
            Log.Information("Block {Height} Is Being Validated by me.");
            chain.Commit();
        
            /*chain.CurrentBlock.Sign(chain.ValidatorKeys);
            var blockString = Base16.Encode(chain.CurrentBlock.ToByteArray(true));
            var transactions = chain.Transactions.ToArray();
            var transactionString = Base16.Encode(transactions.Serialize());

            
            // Broadcast the block
            var rpcBroadcast = "block:" + blockString;
            rpcBroadcast += "_transactions:" + transactionString;
            try
            {
                _rpc.BroadcastBlock(rpcBroadcast);
                Log.Information("Broadcast block {Block}", blockString);
            }
            catch(Exception e)
            {
                Log.Information(e.ToString());
                Log.Error("Something went wrong while broadcasting the block");
            }*/
        }
        else
        {
            AttemptRequestBlock(chain);
            //var data = chain.Commit();
        }
        var response = new ResponseCommit();
        //response.Data = ByteString.CopyFrom(data); // this would change the app hash, we don't want that
        return Task.FromResult(response);
    }

    private Task<byte[]> HandleRequestBlock(Chain chain, Tendermint.RPC.Endpoint.ResponseQuery response)
    {
        if ( response.Code != (int) CodeType.Ok) return Task.FromResult(new byte[0]);
        if ( response.Value == null ) return Task.FromResult(new byte[0]);
        var blockString = ByteString.FromBase64(response.Value).ToStringUtf8();
        var split = blockString.Split("_");
        var blockEncoded = split[0].Split(":")[1];
        var block = Serialization.Unserialize<Block>(Base16.Decode(blockEncoded));
        var transactionsEncoded = split[1].Split(":")[1];
        var transactions =
            Serialization.Unserialize<Transaction[]>(Base16.Decode(transactionsEncoded));
        return Task.FromResult(chain.SetBlock(block, transactions));
    }
    
    private Task<byte[]> AttemptRequestBlock(Chain chain)
    {
        try
        {
            _rpc.Health();
            var numberOfAttemps = 2;
            while (numberOfAttemps > 0)
            {
                try
                {
                    var heightRequestBytes = BitConverter.GetBytes((int)chain.CurrentBlock.Height);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(heightRequestBytes);
                    var heightRequest = string.Concat(((int)chain.CurrentBlock.Height).ToString().Select(c => "3" + c.ToString()));
                    Log.Error("Trying to request this height {height}, {height2}", heightRequest, chain.CurrentBlock.Height);

                    var result = _rpc.RequestBlock(heightRequest);
                    var data =  HandleRequestBlock(chain, result.Response);
                    return data;
                }
                catch ( Exception e)
                {
                    Log.Information(e.ToString());
                    Log.Error("Something went wrong while requesting the block, Attempts {NumberOfAttemps}", numberOfAttemps);
                }
                numberOfAttemps--;
                Thread.Sleep(_delayRequests);
            }
        }
        catch ( Exception e)
        {
            Log.Error("Not connected to tendermint.");
        }
        
        
        return Task.FromResult(new byte[0]);
    }

    public override Task<ResponseEcho> Echo(RequestEcho request, ServerCallContext context)
    {
        var echo = new ResponseEcho();
        echo.Message = request.Message;
        
        // Handle echo
        /*if ( request.Message.Contains("block:") )
        {
            // Handle block
        }*/
        
        Log.Information("Echo " + echo.Message);
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
        Log.Information($"ABCI Connector - Info");

        uint version = DomainSettings.Phantasma30Protocol;

        try 
        {
            lastBlockHash = _nexus.RootChain.GetLastBlockHash();
            lastBlock = _nexus.RootChain.GetBlockByHash(lastBlockHash);
            try
            {
                version = _nexus.GetProtocolVersion(_nexus.RootStorage);
            }catch(Exception e)
            {
                Log.Information("Error getting info {Exception}", e);
            }
        }
        catch (Exception e)
        {
            Log.Information("Error getting info {Exception}", e);
        }

        ResponseInfo response = new ResponseInfo()
        {
            AppVersion = 0,
            LastBlockHeight = (lastBlock != null) ? (long)lastBlock.Height : 0,
            Version = "0.2." + version,
        };

        return Task.FromResult(response);
    }

    public override Task<ResponseInitChain> InitChain(RequestInitChain request, ServerCallContext context)
    {
        Log.Information($"ABCI Connector - Init Chain");

        var response = new ResponseInitChain();
        var timestamp = new Timestamp((uint) request.Time.Seconds);

        try
        {
            var signerAddress = _initialValidators.Last().TendermintAddress;

            _nexus.SetInitialValidators(this._initialValidators);

            if (this._owner.Address.TendermintAddress == signerAddress)
            {
                var tx = _nexus.CreateGenesisTransaction(timestamp, this._owner);

                var txString = Base16.Encode(tx.ToByteArray(true));
                Task.Factory.StartNew(() => _rpc.BroadcastTxSync(txString));

                Log.Information($"Broadcasting genesis tx {tx} signed by {signerAddress}");
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
        Log.Information($"ABCI Connector - Query");
        var query = new ResponseQuery();
        //query.Codespace = "query";
        //query.Code = (int)CodeType.InvalidChain;

        if (request.Path.Contains("/phantasma/block_sync/"))
        {
            if (request.Path.Contains("/get"))
            {
                Log.Information($"ABCI Connector - Query - Getter block");
                try
                {
                    var chain = _nexus.RootChain as Chain;
                    Log.Information("Data is - {Data}", request.Data.ToStringUtf8());

                    var height = int.Parse(request.Data.ToStringUtf8());
                    var hash = chain.GetBlockHashAtHeight(height);
                    
                    if ( hash == Hash.Null )
                    {
                        query.Code = (int)CodeType.Error;
                        query.Info = "Block get";
                        return Task.FromResult(query);
                    }
                    
                    var block = chain.GetBlockByHash(hash);
                    if ( block == null )
                    {
                        query.Code = (int)CodeType.Error;
                        query.Info = "Block get";
                        query.Value = "Block not found".ToByteString();
                        return Task.FromResult(query);
                    }
                    
                    var transactions = chain.GetBlockTransactions(block);
                    if ( transactions == null )
                    {
                        query.Code = (int)CodeType.Error;
                        query.Info = "Block get";
                        query.Value = "Transactions not found".ToByteString();
                        return Task.FromResult(query);
                    }
                    
                    var blockBytes = block.ToByteArray(true);
                    var transactionsBytes = Serialization.Serialize(transactions.ToArray());
                    
                    var response = "block:" + Base16.Encode(blockBytes);
                    response += "_transactions:" + Base16.Encode(transactionsBytes);
                    query.Info = "Block get";
                    query.Value = response.ToByteString();
                    query.Code = (int)CodeType.Ok;
                }
                catch ( Exception e )
                {
                    Log.Information("Error getting block {Exception}", e);
                    query.Info = "Block get";
                    query.Value = e.Message.ToByteString();
                    query.Code = (int)CodeType.Error;
                }
                
            }
            /* Not sure if this is is needed since we request the blocks from the RPC
             else if (request.Path.Contains("/set"))
            {
                var chain = _nexus.RootChain as Chain;
                var split = request.Data.ToStringUtf8().Split("_");
                var blockEncoded = split[0].Split(":")[1];
                var block = Serialization.Unserialize<Block>(Base16.Decode(blockEncoded));
                var transactionsEncoded = split[1].Split(":")[1];
                var transactions =
                    Serialization.Unserialize<IEnumerable<Transaction>>(Base16.Decode(transactionsEncoded));
                chain.SetBlock(block, transactions);
                query.Code = (int)CodeType.Ok;
                query.Info = "Block set";
            }*/
        }

        return Task.FromResult( query );
    }

    public override Task<ResponseListSnapshots> ListSnapshots(RequestListSnapshots request, ServerCallContext context)
    {
        Log.Information($"ABCI Connector - ListSnapshots");

        return Task.FromResult( new ResponseListSnapshots());
    }

    public override Task<ResponseOfferSnapshot> OfferSnapshot(RequestOfferSnapshot request, ServerCallContext context)
    {
        Log.Information($"ABCI Connector - OfferSnapshot");

        return Task.FromResult( new ResponseOfferSnapshot());
    }

    public override Task<ResponseLoadSnapshotChunk> LoadSnapshotChunk(RequestLoadSnapshotChunk request, ServerCallContext context)
    {
        Log.Information($"ABCI Connector - LoadSnapshotChunk");

        return Task.FromResult( new ResponseLoadSnapshotChunk());
    }

    public override Task<ResponseApplySnapshotChunk> ApplySnapshotChunk(RequestApplySnapshotChunk request, ServerCallContext context)
    {
        Log.Information($"ABCI Connector - ApplySnapshotChunk");

        return Task.FromResult( new ResponseApplySnapshotChunk());
    }
}
