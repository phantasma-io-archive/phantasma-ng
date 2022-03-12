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
    private Dictionary<int, Transaction>_systemTxs = new Dictionary<int, Transaction>();

    // TODO add logger
    public ABCIConnector() { Log.Information("ABCI Connector initialized"); }

    public void SetNodeInfo(Nexus nexus, string tendermintEndpoint, PhantasmaKeys keys)
    {
        _owner = keys;
        _nexus = nexus;
        _rpc = new NodeRpcClient(tendermintEndpoint);
    }

    public override Task<ResponseBeginBlock> BeginBlock(RequestBeginBlock request, ServerCallContext context)
    {
        Console.WriteLine("##########################BeginBlock has been called. " + request.Header.Height);
        var response = new ResponseBeginBlock();
        try
        {
            IEnumerable<Transaction> systemTransactions;;
            systemTransactions = _nexus.RootChain.BeginBlock(request.Header); 

            var idx = 0;
            foreach (var tx in systemTransactions)
            {
                Log.Information("Broadcasting system transaction {}", tx);
                // add system tx to sorted set, to make sure we have processed the transactions.
                _systemTxs.Add(idx, tx);
                var txString = Base16.Encode(tx.ToByteArray(false));
                Task.Factory.StartNew(() => _rpc.BroadcastTxSync(txString));
                idx++;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return Task.FromResult(response);
    }
    
    public override Task<ResponseCheckTx> CheckTx(RequestCheckTx request, ServerCallContext context)
    {
        Log.Debug("CheckTx called");
        (CodeType code, string message) = _nexus.RootChain.CheckTx(request.Tx);
        Log.Debug("CheckTx code {} message {}", code, message);

        var response = new ResponseCheckTx();
        if (code == CodeType.Ok)    
        {
            return Task.FromResult(ResponseHelper.Check.Ok());
        }

        return Task.FromResult(ResponseHelper.Check.Create((CodeType)code, message));
    }
    
    public override Task<ResponseDeliverTx> DeliverTx(RequestDeliverTx request, ServerCallContext context)
    {
        Log.Debug("DeliverTx called");
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

        return Task.FromResult(response);
    }

    public override Task<ResponseEndBlock> EndBlock(RequestEndBlock request, ServerCallContext context)
    {
        Log.Debug("EndBlock called.");
        var result = _nexus.RootChain.EndBlock();
        var response = new ResponseEndBlock();

        response.ValidatorUpdates.AddRange(result);

        // TODO
        //response.ConsensusParamUpdates = ???
        //response.Events = ???

        return Task.FromResult(response);
    }
    
    public override Task<ResponseCommit> Commit(RequestCommit request, ServerCallContext context)
    {
        Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!1Commit has been called.");
        var data = _nexus.RootChain.Commit();
        var response = new ResponseCommit();
        response.Data = ByteString.CopyFrom(data);
        return Task.FromResult(response);
    }


    public override Task<ResponseEcho> Echo(RequestEcho request, ServerCallContext context)
    {
        Log.Information("[Echo] " + request.Message);
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

            //Console.WriteLine("Last block hash: " + lastBlockHash);
            //Console.WriteLine("Last block height: " + lastBlock?.Height);
            response = new ResponseInfo() {
                AppVersion = _nexus.GetProtocolVersion(_nexus.RootStorage),
                LastBlockAppHash = ByteString.CopyFrom(_nexus.GetProtocolVersion(_nexus.RootStorage).ToString(), Encoding.UTF8) ,

                //LastBlockHeight = (lastBlock != null) ? (long)lastBlock.Height : 0,
                LastBlockHeight = 0, // this needs to be tendermint height, calculate tendermint height from first block onwards
                Version = "0.0.1",
            };
        }
        catch (Exception e)
        {
            Console.WriteLine("Error getting info " + e);
        }

        //Console.WriteLine("Info has been called. " + response.AppVersion);

        return Task.FromResult(response);
    }

    public override Task<ResponseInitChain> InitChain(RequestInitChain request, ServerCallContext context)
    {
        Console.WriteLine("##########################InitChain has been called. " + request.InitialHeight);
        var response = new ResponseInitChain();
        //var script = Base16.Decode("0D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00040F4E657875732E426567696E496E697407000D00040976616C696461746F7203000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D00040A676F7665726E616E636503000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D000409636F6E73656E73757303000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D0004076163636F756E7403000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D00040865786368616E676503000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D0004047377617003000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D000407696E7465726F7003000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D0004057374616B6503000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D00040773746F7261676503000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D00040572656C617903000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D00040772616E6B696E6703000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D0004077072697661637903000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D0004046D61696C03000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D000407667269656E647303000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D0004066D61726B657403000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D00040473616C6503000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041652756E74696D652E4465706C6F79436F6E747261637407000D00020003000D00040F426C6F636B2050726F64756365727303000D00040A76616C696461746F727303000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D0004184E657875732E4372656174654F7267616E697A6174696F6E07000D00020003000D00040C536F756C204D61737465727303000D0004076D61737465727303000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D0004184E657875732E4372656174654F7267616E697A6174696F6E07000D00020003000D00040C536F756C205374616B65727303000D0004077374616B65727303000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D0004184E657875732E4372656174654F7267616E697A6174696F6E07000D00030800CA0CFD7104010003000D000404534F554C03000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041252756E74696D652E4D696E74546F6B656E7307000D0003080000C16FF286230003000D0004044B43414C03000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00041252756E74696D652E4D696E74546F6B656E7307000D000307005039278C040003000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D0004055374616B6503000D0004057374616B652D00012E010D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D000405436C61696D03000D0004057374616B652D00012E010D000223220100AC42F8B9E617BE1524893A76A1B0CCF937782023BA35301948A8F94CEBC67A1F03000D00040D4E657875732E456E64496E697407000B0B");

        ////Console.WriteLine("decoded: " + string.Join("", script));
        //var x = new Disassembler(script);
        //foreach (var a in x.Instructions)
        //{
        //    Console.WriteLine("instr: " + a);
        //}
        //Thread.Sleep(10000000);
        //try
        //{
        //    IEnumerable<Transaction> systemTransactions;;
        //    var timestamp = new Timestamp((uint) request.Time.Seconds);
        //    //var success = _nexus.CreateGenesisBlock(timestamp, request.ChainId, 0, this._owner);
        //    //foreach (var tx in systemTransactions)
        //    //{
        //    //    Console.WriteLine("tx: " + tx.Hash);

        //    //}

        //    var genesisBlock = new Block(1
        //        , _nexus.RootChain.Address
        //        , Timestamp.Now
        //        , Hash.Null
        //        , 0
        //        , _nexus.RootChain.ValidatorAddress
        //        , new byte[0]);

        //    var oracle = new BlockOracleReader(_nexus, genesisBlock);
        //    var changeSet = _nexus.RootChain.ProcessBlock(block, transactions, 1, out inflationTx, owner);
        //    //var changeSet = _nexus.RootChain.ProcessTransactions(genesisBlock, systemTransactions, oracle, 1);
        //    
        //    //var idx = 0;
        //    //foreach (var tx in systemTransactions)
        //    //{
        //    //    Log.Information("Broadcasting system transaction {}", tx);
        //    //    // add system tx to sorted set, to make sure we have processed the transactions.
        //    //    _systemTxs.Add(idx, tx);
        //    //    var txString = Base16.Encode(tx.ToByteArray(false));
        //    //    Task.Factory.StartNew(() => _rpc.BroadcastTxSync(txString));
        //    //    idx++;
        //    //}
        //}
        //catch (Exception e)
        //{
        //    Console.WriteLine(e);
        //}

        // NEED THAT: 
        try
        {
            //Console.WriteLine("app state bytes: " + request.AppStateBytes.Length);
            var json = JsonDocument.Parse(Encoding.UTF8.GetString(request.AppStateBytes.ToByteArray()));
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
               json.RootElement.GetProperty("current_state").WriteTo(writer);
            }
            
            var formatted = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            //Console.WriteLine("app state bytes : " + formatted.Substring(0, 100));
            var x = JsonSerializer.Deserialize<Dictionary<string, string>>(json.RootElement.GetProperty("current_state"));

            foreach (var kvp in x)
            {
                var bytes = CompressionUtils.Decompress(Base16.Decode(kvp.Value));
                var blockList = SerializedBlockList.Deserialize(bytes);
                //foreach (var block in blockList.Blocks.Skip(76))
                foreach (var block in blockList.Blocks)
                {
                    if (block.Value.Height < 21385)
                    {
                        //Console.WriteLine("DONE");
                        //Environment.Exit(0);
                        continue;
                    }

                    if (block.Value.Height > 21385)
                    {
                        Console.WriteLine("DONE");
                        Environment.Exit(0);
                        //continue;
                    }
                    //Console.WriteLine("block size: " + block.Value.ToByteArray(false).Length);
                    var height = block.Value.Height;
                    //Console.WriteLine("height: " + height + " signed: " + block.Value.IsSigned);
                    var txs = blockList.BlockTransactions[height];
                    var sortedTxs = new List<Transaction>();

                    for (var i = 0; i < txs.Count; i++)
                    {
                        sortedTxs.Add(txs[i]);
                    }

                    var oracle = new BlockOracleReader(_nexus, block.Value);
                    //Console.WriteLine("PRocessblock start" + _nexus.RootChain.ValidatorAddress);
                    //Console.WriteLine("block bytes: " + string.Join(" ", block.Value.ToByteArray(false)));
                    //Console.WriteLine("before block : " + block.Value.ToByteArray(false).Length);
                    Console.WriteLine("ProcessBlock " + block.Value.Height);
                    var changeSet = _nexus.RootChain.ProcessTransactions(block.Value, sortedTxs, oracle, 1);
                    //Console.WriteLine("after block : " + block.Value.ToByteArray(false).Length);
                    //Console.WriteLine("PRocessblock done ");
                    //var changeSet = _nexus.RootChain.ProcessTransactions(block.Value, sortedTxs, oracle, 1);
                    //Console.WriteLine("verify block: " + block.Value.Signature.Verify(block.Value.ToByteArray(false), block.Value.Validator));
                    Console.WriteLine("AddBlock " + block.Value.Height);
                    _nexus.RootChain.AddBlock(block.Value, sortedTxs, 1, changeSet);
                    if (block.Value.Height == 1)
                    {
                        var storage = _nexus.RootStorage;
                        var key = System.Text.Encoding.UTF8.GetBytes($".nexus.hash");
                        storage.Put(bytes, block.Value.Hash);

                        _nexus.HasGenesis = true;
                    }
                    //var nblock = _nexus.RootChain.GetBlockByHash(_nexus.RootChain.GetBlockHashAtHeight(1));
                    //Console.WriteLine("ADDED BLOCK:::::::::::: " + nblock.Height);
                }
            }

            var timestamp = new Timestamp((uint) request.Time.Seconds);
            var appHash = Encoding.UTF8.GetBytes("A Phantasma was born...");
            response.AppHash = ByteString.CopyFrom(appHash);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error initializing chain " + e);
            throw;
        }

        Console.WriteLine("##########################InitChain Done . " + request.InitialHeight);
        return Task.FromResult( response );
    }

    public override Task<ResponseQuery> Query(RequestQuery request, ServerCallContext context)
    {
        Console.WriteLine("Query has been called.");
        return Task.FromResult( new ResponseQuery());
    }

    public override Task<ResponseListSnapshots> ListSnapshots(RequestListSnapshots request, ServerCallContext context)
    {
        Console.WriteLine("Query has been called.");
        return Task.FromResult( new ResponseListSnapshots());
    }

    public override Task<ResponseOfferSnapshot> OfferSnapshot(RequestOfferSnapshot request, ServerCallContext context)
    {
        Console.WriteLine("Query has been called.");
        return Task.FromResult( new ResponseOfferSnapshot());
    }

    public override Task<ResponseLoadSnapshotChunk> LoadSnapshotChunk(RequestLoadSnapshotChunk request, ServerCallContext context)
    {
        Console.WriteLine("Query has been called.");
        return Task.FromResult( new ResponseLoadSnapshotChunk());
    }

    public override Task<ResponseApplySnapshotChunk> ApplySnapshotChunk(RequestApplySnapshotChunk request, ServerCallContext context)
    {
        Console.WriteLine("Query has been called.");
        return Task.FromResult( new ResponseApplySnapshotChunk());
    }
}
