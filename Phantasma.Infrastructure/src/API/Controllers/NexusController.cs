using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class NexusController : BaseControllerV1
    {
        [APIInfo(typeof(NexusResult), "Returns info about the nexus.", false, 60)]
        [HttpGet("GetNexus")]
        public NexusResult GetNexus(bool extended = false)
        {
            var nexus = NexusAPI.GetNexus();

            var tokenList = new List<TokenResult>();

            var symbols = nexus.GetTokens(nexus.RootStorage);
            foreach (var token in symbols)
            {
                var entry = NexusAPI.FillToken(token, false, extended);
                tokenList.Add(entry);
            }

            var platformList = new List<PlatformResult>();

            var platforms = nexus.GetPlatforms(nexus.RootStorage);
            foreach (var platform in platforms)
            {
                var info = nexus.GetPlatformInfo(nexus.RootStorage, platform);

                var entry = new PlatformResult();
                entry.platform = platform;
                entry.interop = info.InteropAddresses.Select(x => new InteropResult()
                {
                    local = x.LocalAddress.Text,
                    external = x.ExternalAddress
                }).ToArray();
                entry.chain = DomainExtensions.GetChainAddress(info).Text;
                entry.fuel = info.Symbol;
                entry.tokens = symbols.Where(x => nexus.HasTokenPlatformHash(x, platform, nexus.RootStorage)).ToArray();
                platformList.Add(entry);
            }

            var chainList = new List<ChainResult>();

            var chains = nexus.GetChains(nexus.RootStorage);
            foreach (var chainName in chains)
            {
                var chain = nexus.GetChainByName(chainName);
                var single = NexusAPI.FillChain(chain);
                chainList.Add(single);
            }

            var governance = (GovernancePair[])nexus.RootChain.InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, "governance", nameof(GovernanceContract.GetValues)).ToArray<GovernancePair>();

            var orgs = nexus.GetOrganizations(nexus.RootStorage);

            return new NexusResult()
            {
                name = nexus.Name,
                protocol = nexus.GetProtocolVersion(nexus.RootStorage),
                tokens = tokenList.ToArray(),
                platforms = platformList.ToArray(),
                chains = chainList.ToArray(),
                organizations = orgs,
                governance = governance.Select(x => new GovernanceResult() { name = x.Name, value = x.Value.ToString() }).ToArray()
            };
        }
    }
}
