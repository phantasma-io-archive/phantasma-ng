using Microsoft.AspNetCore.Mvc;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class SwapController : BaseControllerV1
    {
        [APIInfo(typeof(string), "Tries to settle a pending swap for a specific hash.", false, 0, true)]
        [HttpGet("SettleSwap")]
        public string SettleSwap(
            [APIParameter("Name of platform where swap transaction was created", "phantasma")] string sourcePlatform
            , [APIParameter("Name of platform to settle", "phantasma")] string destPlatform
            , [APIParameter("Hash of transaction to settle", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.SettleSwap(sourcePlatform, destPlatform, hashText);
        }

        [APIInfo(typeof(SwapResult[]), "Returns platform swaps for a specific address.", false, 0, true)]
        [HttpGet("GetSwapsForAddress")]
        public SwapResult[] GetSwapsForAddress([APIParameter("Address or account name", "helloman")] string account,
            string platform, bool extended = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetSwapsForAddress(account, platform, extended);
        }
    }
}
