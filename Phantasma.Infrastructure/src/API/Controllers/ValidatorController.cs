using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Infrastructure.API.Structs;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class ValidatorController : BaseControllerV1
    {
        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        [HttpGet("GetValidators")]
        public ValidatorResult[] GetValidators()
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var validators = service.GetValidators().Where(x => !x.address.IsNull).Select(x =>
                new ValidatorResult() { address = x.address.ToString(), type = x.type.ToString() });
            return validators.ToArray();
        }

        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        [HttpGet("GetValidators/{type}")]
        public ValidatorResult[] GetValidators([APIParameter("Validator Type.", "Primary")] string type)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            var validators = service.GetValidators()
                .Where(x => !x.address.IsNull && x.type.ToString() == type).Select(x => new ValidatorResult()
                    { address = x.address.ToString(), type = x.type.ToString() });
            return validators.ToArray();
        }
    }
}
