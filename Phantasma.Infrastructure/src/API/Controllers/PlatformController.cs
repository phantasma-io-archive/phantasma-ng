using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Domain;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class PlatformController : BaseControllerV1
    {
        [APIInfo(typeof(PlatformResult[]), "Returns an array of available interop platforms.", false, 300)]
        [HttpGet("GetPlatforms")]
        public PlatformResult[] GetPlatforms()
        {
            var platformList = new List<PlatformResult>();

            var nexus = NexusAPI.GetNexus();

            var platforms = nexus.GetPlatforms(nexus.RootStorage);
            var symbols = nexus.GetTokens(nexus.RootStorage);

            foreach (var platform in platforms)
            {
                var info = nexus.GetPlatformInfo(nexus.RootStorage, platform);
                var entry = new PlatformResult();
                entry.platform = platform;
                entry.interop = info.InteropAddresses.Select(x => new InteropResult()
                {
                    local = x.LocalAddress.Text,
                    external = x.ExternalAddress
                }).Reverse().ToArray();
                //TODO reverse array for now, only the last item is valid for now.
                entry.chain = DomainExtensions.GetChainAddress(info).Text;
                entry.fuel = info.Symbol;
                entry.tokens = symbols.Where(x => nexus.HasTokenPlatformHash(x, platform, nexus.RootStorage)).ToArray();
                platformList.Add(entry);
            }

            return platformList.ToArray();
        }
    }
}
