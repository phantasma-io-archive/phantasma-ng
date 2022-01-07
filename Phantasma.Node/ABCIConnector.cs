using System;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Tendermint.Abci;

namespace Phantasma.Spook
{
    public class ABCIConnector : ABCIApplication.ABCIApplicationBase
    {
        //private readonly TransactionHandlerRouter _transactionHandlerRouter;
        //private readonly QueryProcessor _queryProcessor;
        //private readonly BlockHandler _blockHandler;

        //public ABCIConnector(TransactionHandlerRouter transactionHandlerRouter, QueryProcessor queryProcessor, BlockHandler blockHandler)
        public ABCIConnector()
        {
            //_transactionHandlerRouter = transactionHandlerRouter;
            //_queryProcessor = queryProcessor;
            //_blockHandler = blockHandler;
        }

        public override Task<ResponseCheckTx> CheckTx(RequestCheckTx request, ServerCallContext context)
        {
            Console.WriteLine("CheckTx has been called.");
            return new Task<ResponseCheckTx>(() => { return new ResponseCheckTx(); });
        }

        public override Task<ResponseBeginBlock> BeginBlock(RequestBeginBlock request, ServerCallContext context)
        {
            Console.WriteLine("BeginBlock has been called.");
            return new Task<ResponseBeginBlock>(() => { return new ResponseBeginBlock(); });
        }

        public override Task<ResponseCommit> Commit(RequestCommit request, ServerCallContext context)
        {
            Console.WriteLine("Commit has been called.");
            return new Task<ResponseCommit>(() => { return new ResponseCommit(); });
        }

        public override Task<ResponseDeliverTx> DeliverTx(RequestDeliverTx request, ServerCallContext context)
        {
            Console.WriteLine("DeliverTx has been called.");
            return new Task<ResponseDeliverTx>(() => { return new ResponseDeliverTx(); });
        }

        public override Task<ResponseEcho> Echo(RequestEcho request, ServerCallContext context)
        {
            Console.WriteLine("Echo has been called. " + request.Message);
            var echo = new ResponseEcho();
            echo.Message = "World";
            return Task.FromResult(echo);
        }

        public override Task<ResponseEndBlock> EndBlock(RequestEndBlock request, ServerCallContext context)
        {
            Console.WriteLine("EndBlock has been called.");
            return new Task<ResponseEndBlock>(() => { return new ResponseEndBlock(); });
        }

        public override Task<ResponseFlush> Flush(RequestFlush request, ServerCallContext context)
        {
            Console.WriteLine("RequestFlush has been called.");
            return new Task<ResponseFlush>(() => { return new ResponseFlush(); });
        }

        public override Task<ResponseInfo> Info(RequestInfo request, ServerCallContext context)
        {
            Console.WriteLine("RequestInfo has been called.");
            return Task.FromResult(new ResponseInfo());
            //return new Task<ResponseInfo>(() => { return new ResponseInfo(); });
        }

        public override Task<ResponseInitChain> InitChain(RequestInitChain request, ServerCallContext context)
        {
            //Console.WriteLine("InitChain has been called. " + request.AppStateBytes.Length);
            Console.WriteLine("string: " + Encoding.UTF8.GetString(request.AppStateBytes.ToByteArray()));
            // TODO 
            // read AbbStateBytes and deserialize json to object
            return Task.FromResult(new ResponseInitChain());
        }

        public override Task<ResponseQuery> Query(RequestQuery request, ServerCallContext context)
        {
            Console.WriteLine("Query has been called.");
            return new Task<ResponseQuery>(() => new ResponseQuery());
        }
    }
}
