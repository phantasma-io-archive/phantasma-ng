using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Domain;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class LeaderboardController : BaseControllerV1
    {
        [APIInfo(typeof(LeaderboardResult), "Returns content of a Phantasma leaderboard.", false, 30)]
        [HttpGet("GetLeaderboard")]
        public LeaderboardResult GetLeaderboard(string name)
        {
            var nexus = NexusAPI.GetNexus();

            var temp = nexus.RootChain.InvokeContract(nexus.RootChain.Storage, "ranking", nameof(RankingContract.GetRows), name).ToObject();

            try
            {
                var board = ((LeaderboardRow[])temp).ToArray();

                return new LeaderboardResult()
                {
                    name = name,
                    rows = board.Select(x => new LeaderboardRowResult() { address = x.address.Text, value = x.score.ToString() }).ToArray(),
                };
            }
            catch (Exception e)
            {
                throw new APIException($"error fetching leaderboard: {e.Message}");
            }

        }
    }
}
