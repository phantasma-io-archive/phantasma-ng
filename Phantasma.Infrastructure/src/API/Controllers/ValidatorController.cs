using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ValidatorController : BaseControllerV1
    {
        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        [HttpGet("GetValidators")]
        public ValidatorResult[] GetValidators()
        {
            var nexus = NexusAPI.GetNexus();
            
            if (nexus == null)
            {
                throw new APIException("Nexus not ready");
            }

            var validators = nexus.GetValidators(Timestamp.Now).
                Where(x => !x.address.IsNull).
                Select(x => new ValidatorResult() { address = x.address.ToString(), type = x.type.ToString() });

            return validators.ToArray();
        }
        
        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        [HttpGet("GetValidators/{type}")]
        public ValidatorResult[] GetValidators(string type)
        {
            var nexus = NexusAPI.GetNexus();

            if (nexus == null)
            {
                throw new APIException("Nexus not ready");
            }
            
            var validators = nexus.GetValidators(Timestamp.Now).
                Where(x => !x.address.IsNull && x.type.ToString() == type).
                Select(x => new ValidatorResult() { address = x.address.ToString(), type = x.type.ToString() });

            return validators.ToArray();
        }
        
        
    }
}
