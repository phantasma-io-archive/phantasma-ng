using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class SwapController : BaseControllerV1
    {
        [APIInfo(typeof(string), "Tries to settle a pending swap for a specific hash.", false, 0, true)]
        [HttpGet("SettleCrossChainSwap")]
        public string SettleCrossChainSwap(string address,
            [APIParameter("Name of platform where swap transaction was created", "phantasma")] string sourcePlatform
            , [APIParameter("Name of platform to settle", "phantasma")] string destPlatform
            , [APIParameter("Hash of transaction to settle", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            if (NexusAPI.ReadOnlyMode)
            {
                return "Chain is in Read only mode";
            }
            
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.SettleCrossChainSwap(address, sourcePlatform, destPlatform, hashText);
        }

        [APIInfo(typeof(TokenSwapToSwap[]), "Returns all the platforms with all the swappers for a specific platform.",
            false, 0, true)]
        [HttpGet("GetSwappersForPlatform")]
        public TokenSwapToSwap[] GetSwappersForPlatform(string platform)
        {
            if (NexusAPI.ReadOnlyMode)
            {
                return null;
            }
            
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetSwappersForPlatform(platform);
        }

        [APIInfo(typeof(PlatformDetails), "Returns all the platforms with all the swappers for a specific platform.",
            false, 0, true)]
        [HttpGet("GetPlatformDetails")]
        public PlatformDetails GetPlatformDetails(string address, string platform)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetPlatformDetails(address, platform);
        }

        [APIInfo(typeof(CrossChainTransfer[]), "Returns platform swaps for a specific address.", false, 0, true)]
        [HttpGet("GetSwapsForAddress")]
        public CrossChainTransfer[] GetSwapsForAddress(
            [APIParameter("Address or account name", "helloman")] string account,
            string platform, bool extended = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetSwapsForAddress(account, platform, extended);
        }
    }
}
