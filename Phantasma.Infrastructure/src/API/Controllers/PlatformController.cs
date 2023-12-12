using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Domain;
using Phantasma.Infrastructure.API.Structs;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class PlatformController : BaseControllerV1
    {
        [APIInfo(typeof(PlatformResult[]), "Returns an array of available interop platforms.", false, 300)]
        [HttpGet("GetPlatforms")]
        public PlatformResult[] GetPlatforms()
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var platformList = new List<PlatformResult>();
            var platforms = service.GetPlatforms();
            var symbols = service.GetAvailableTokenSymbols();

            foreach (var platform in platforms)
            {
                var info = service.GetPlatformInfo(platform);
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
                entry.tokens = symbols.Where(x => service.HasTokenPlatformHash(x, platform)).ToArray();
                platformList.Add(entry);
            }

            return platformList.ToArray();
        }
        
        [APIInfo(typeof(PlatformResult), "Returns the platform info for the given platform.", false, 300)]
        [HttpGet("GetPlatform")]
        public PlatformResult GetPlatform([APIParameter( "Platform name", "ethereum")]string platform)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var info = service.GetPlatformInfo(platform);
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
            entry.tokens = service.GetAvailableTokenSymbols().Where(x => 
                service.HasTokenPlatformHash(x, platform)).ToArray();
            return entry;
        }
        
        [APIInfo(typeof(InteropResult), "Returns the interop info for the given platform.", false, 300)]
        [HttpGet("GetInterop")]
        public InteropResult GetInterop([APIParameter( "Platform name.", "ethereum")]string platform)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var info = service.GetPlatformInfo(platform);
            var entry = new InteropResult();
            entry.local = DomainExtensions.GetChainAddress(info).Text;
            entry.external = info.InteropAddresses.Last().ExternalAddress;
            return entry;
        }
    }
}
