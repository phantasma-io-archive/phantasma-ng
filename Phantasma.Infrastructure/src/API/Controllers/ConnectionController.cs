using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Domain;
using Phantasma.Infrastructure.Utilities;
using Tendermint.RPC.Endpoint;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ConnectionController : BaseControllerV1
    {
        [APIInfo(typeof(ResultAbciQuery), "Returns a abci query.", false, 300)]
        [HttpGet("abci_query")]
        public ResultAbciQuery AbciQuery(string path, string data = null, int height = 0, bool prove = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.AbciQuery(path, data, height, prove);
        }
        
        [APIInfo(typeof(ResultHealth), "Returns the health of tendermint.", false, 300)]
        [HttpGet("health")]
        public ResultHealth Health()
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.Health();
        }
        
        [APIInfo(typeof(ResultStatus), "Returns the status of tendermint.", false, 300)]
        [HttpGet("status")]
        public ResultStatus Status()
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.Status();
        }
        
        [APIInfo(typeof(ResultNetInfo), "Returns a net info.", false, 300)]
        [HttpGet("net_info")]
        public ResultNetInfo NetInfo()
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.NetInfo();
        }


        [APIInfo(typeof(ResultAbciQuery), "Returns a abci query.", false, 300)]
        [HttpGet("request_block")]
        public ResultAbciQuery RequestBlock(int height = 0)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.RequestBlock(height);
        }
        
        [APIInfo(typeof(List<ValidatorSettings>), "Returns a Validator settings.", false, 300)]
        [HttpGet("GetValidatorsSettings")]
        public List<ValidatorSettings> GetValidatorSettings()
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.Validators;
        }
        
    }
}
