using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Phantasma.Infrastructure.Controllers
{
    public class ChainController : BaseControllerV1
    {
        [APIInfo(typeof(ChainResult[]), "Returns an array of all chains deployed in Phantasma.", false, 300)]
        [HttpGet("GetChains")]
        public ChainResult[] GetChains()
        {
            var objs = new List<ChainResult>();

            var nexus = NexusAPI.GetNexus();

            var chains = nexus.GetChains(nexus.RootStorage);
            foreach (var chainName in chains)
            {
                var chain = nexus.GetChainByName(chainName);
                var single = NexusAPI.FillChain(chain);
                objs.Add(single);
            }

            return objs.ToArray();
        }
    }
}
