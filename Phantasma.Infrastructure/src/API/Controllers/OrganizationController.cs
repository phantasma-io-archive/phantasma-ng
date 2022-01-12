using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Phantasma.Infrastructure.Controllers
{
    public class OrganizationController : BaseControllerV1
    {
        [APIInfo(typeof(OrganizationResult), "Returns info about an organization.", false, 60)]
        [HttpGet("GetOrganization")]
        public OrganizationResult GetOrganization(string ID)
        {
            var nexus = NexusAPI.GetNexus();

            if (!nexus.OrganizationExists(nexus.RootStorage, ID))
            {
                throw new APIException("invalid organization");
            }

            var org = nexus.GetOrganizationByName(nexus.RootStorage, ID);
            var members = org.GetMembers();

            return new OrganizationResult()
            {
                id = ID,
                name = org.Name,
                members = members.Select(x => x.Text).ToArray(),
            };
        }
    }
}
