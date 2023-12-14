using Microsoft.AspNetCore.Mvc;
using Phantasma.Infrastructure.API.Structs;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class LeaderboardController : BaseControllerV1
    {
        [APIInfo(typeof(LeaderboardResult), "Returns content of a Phantasma leaderboard.", false, 30)]
        [HttpGet("GetLeaderboard")]
        public LeaderboardResult GetLeaderboard([APIParameter( "Name of the leaderboard.", "katacomb")]string name)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetLeaderboard(name);
        }
    }
}
