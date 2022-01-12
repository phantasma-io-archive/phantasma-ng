using Microsoft.AspNetCore.Mvc;

namespace Phantasma.Infrastructure.Controllers;

[ApiController]
[Produces("application/json")]
[Route("")]
public abstract class BaseRpcControllerV1 : ControllerBase
{
}
