using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ValidatorController : BaseControllerV1
    {
        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        [HttpGet("GetValidators")]
        public ValidatorResult[] GetValidators()
        {
            var nexus = NexusAPI.GetNexus();

            var validators = nexus.GetValidators().
                Where(x => !x.address.IsNull).
                Select(x => new ValidatorResult() { address = x.address.ToString(), type = x.type.ToString() });

            return validators.ToArray();
        }
    }
}
