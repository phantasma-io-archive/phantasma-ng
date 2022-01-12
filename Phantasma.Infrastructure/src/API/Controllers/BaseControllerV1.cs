using Microsoft.AspNetCore.Mvc;

namespace Phantasma.Infrastructure.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/v1")]
public abstract class BaseControllerV1 : ControllerBase
{
}
