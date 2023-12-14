using Microsoft.AspNetCore.Mvc;
using Phantasma.Infrastructure.API.Interfaces;

namespace Phantasma.Infrastructure.API
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/v1")]
    public abstract class BaseControllerV1 : ControllerBase
    {
        protected IAPIService APIService => HttpContext.Items["APIService"] as IAPIService;
    }

}
