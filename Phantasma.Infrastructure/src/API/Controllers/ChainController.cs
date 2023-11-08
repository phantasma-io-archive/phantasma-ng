using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ChainController : BaseControllerV1
    {
        // extended parameter added during the transition towards removing transaction details from GetChains
        [APIInfo(typeof(ChainResult[]), "Returns an array of all chains deployed in Phantasma.", false, 300)]
        [HttpGet("GetChains")]
        public ChainResult[] GetChains(
            [APIParameter(
                description:
                "Extended data. Returns all contracts. (deprecated, will be removed in future versions)",
                value: "true")]
            bool extended = true)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var chains = service.GetChains();

            return chains.Select(chainName => service.GetChainByName(chainName))
                .Select(chain => service.FillChain(chain, extended)).ToArray();
        }

        public ChainResult GetChain(
            [APIParameter(description: "Name of chain", value: "main")]
            string name = "main",
            [APIParameter(description: "Extended data. Returns all contracts.", value: "true")]
            bool extended = true)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var chain = service.GetChainByName(name);
            return service.FillChain(chain, extended);
        }
    }
}
