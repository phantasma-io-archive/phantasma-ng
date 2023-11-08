using Microsoft.AspNetCore.Mvc;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class NexusController : BaseControllerV1
    {
        [APIInfo(typeof(NexusResult), "Returns info about the nexus.", false, 60)]
        [HttpGet("GetNexus")]
        public NexusResult GetNexus([APIParameter(description: "Extended data. Returns contracts and scripts.", value: "false")]
            bool extended = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetNexus(extended);
        }
    }
}
