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
        
        [APIInfo(typeof(PlatformResult), "Returns the platform info for the given platform.", false, 300)]
        [HttpGet("GetPlatform")]
        public PlatformResult GetPlatform(string platform)
        {
            var nexus = NexusAPI.GetNexus();

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
            entry.tokens = nexus.GetTokens(nexus.RootStorage).Where(x => nexus.HasTokenPlatformHash(x, platform, nexus.RootStorage)).ToArray();
            return entry;
        }
        
        [APIInfo(typeof(InteropResult), "Returns the interop info for the given platform.", false, 300)]
        [HttpGet("GetInterop")]
        public InteropResult GetInterop(string platform)
        {
            var nexus = NexusAPI.GetNexus();
            
            if ( nexus == null )
            {
                throw new APIException("nexus not found");;
            }
            
            /*if( nexus.GetPlatformInfo(nexus.RootStorage, platform) == null )
            {
                throw new APIException("platform not found");
            }*/
            
            var info = nexus.GetPlatformInfo(nexus.RootStorage, platform);
            var entry = new InteropResult();
            entry.local = DomainExtensions.GetChainAddress(info).Text;
            entry.external = info.InteropAddresses.Last().ExternalAddress;
            return entry;
        }
    }
}
