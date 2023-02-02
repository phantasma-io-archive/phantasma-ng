using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Math.EC;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Tendermint.RPC.Endpoint;
using Tendermint.RPC;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ConnectionController : BaseControllerV1
    {
        [APIInfo(typeof(ResultAbciQuery), "Returns a abci query.", false, 300)]
        [HttpGet("abci_query")]
        public ResultAbciQuery AbciQuery(string path, string data = null, int height = 0, bool prove = false)
        {
            var rpcClient = NexusAPI.TRPC;

            var result = rpcClient.AbciQuery(path, data, height, prove);

            return result;
        }
        
        [APIInfo(typeof(ResultHealth), "Returns the health of tendermint.", false, 300)]
        [HttpGet("health")]
        public ResultHealth Health()
        {
            var rpcClient = NexusAPI.TRPC;

            var result = rpcClient.Health();

            return result;
        }
        
        [APIInfo(typeof(ResultStatus), "Returns the status of tendermint.", false, 300)]
        [HttpGet("status")]
        public ResultStatus Status()
        {
            var rpcClient = NexusAPI.TRPC;

            var result = rpcClient.Status();

            return result;
        }
        
        [APIInfo(typeof(ResultNetInfo), "Returns a net info.", false, 300)]
        [HttpGet("net_info")]
        public ResultNetInfo NetInfo()
        {
            var rpcClient = NexusAPI.TRPC;

            var result = rpcClient.NetInfo();

            return result;
        }


        [APIInfo(typeof(ResultAbciQuery), "Returns a abci query.", false, 300)]
        [HttpGet("request_block")]
        public ResultAbciQuery RequestBlock(int height = 0)
        {
            var rpcClient = NexusAPI.TRPC;
            ResultAbciQuery result = new ResultAbciQuery();
            try
            {
                result = rpcClient.RequestBlock(height.ToString());
            }
            catch(Exception e)
            {
                try
                {
                    result.Response = new ResponseQuery();
                    var chain = NexusAPI.Nexus.RootChain as Chain;
                    var blockHash = chain.GetBlockHashAtHeight(height);
                    var block = chain.GetBlockByHash(blockHash);
                    var transactions = chain.GetBlockTransactions(block);
                    var blockBytes = block.ToByteArray(true);
                    var transactionsBytes = Serialization.Serialize(transactions.ToArray());
                    
                    var response = "block:" + Base16.Encode(blockBytes);
                    response += "_transactions:" + Base16.Encode(transactionsBytes);
                
                    result.Response.Code = 0;
                    result.Response.Info = "Block get";
                    result.Response.Value = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(response));
                }
                catch (Exception ex)
                {
                    result.Response = new ResponseQuery();

                    result.Response.Code = 1;
                    result.Response.Info = "Block not found";
                    result.Response.Value = null;
                }
               
            }


            return result;
        }
        
        [APIInfo(typeof(List<ValidatorSettings>), "Returns a Validator settings.", false, 300)]
        [HttpGet("GetValidatorsSettings")]
        public List<ValidatorSettings> GetValidatorSettings()
        {
            return NexusAPI.Validators;
        }
        
    }
}
