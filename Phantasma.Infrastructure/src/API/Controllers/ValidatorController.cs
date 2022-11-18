using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Types;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ValidatorController : BaseControllerV1
    {
        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        [HttpGet("GetValidators")]
        public ValidatorResult[] GetValidators()
        {
            var nexus = NexusAPI.GetNexus();

            var validators = nexus.GetValidators(Timestamp.Now).
                Where(x => !x.address.IsNull).
                Select(x => new ValidatorResult() { address = x.address.ToString(), type = x.type.ToString() });

            return validators.ToArray();
        }
    }
}
