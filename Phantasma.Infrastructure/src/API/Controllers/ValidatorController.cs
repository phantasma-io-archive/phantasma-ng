using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core;

namespace Phantasma.Infrastructure.Controllers
{
    public class ValidatorController : BaseControllerV1
    {
        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        [HttpGet("GetValidators")]
        public async Task<ValidatorResult[]> GetValidators()
        {
            var nexus = NexusAPI.GetNexus();

            var validators = from x in await nexus.GetValidators()
                             where !x.address.IsNull
                             select new ValidatorResult() 
                             { 
                                 address = x.address.ToString(), 
                                 type = x.type.ToString() 
                             };

            return validators.ToArray();
        }
    }
}
