using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Phantasma.Infrastructure.API.Controllers
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
        
        [APIInfo(typeof(OrganizationResult), "Returns info about an organization.", false, 60)]
        [HttpGet("GetOrganizationByName")]
        public OrganizationResult GetOrganizationByName(string name)
        {
            var nexus = NexusAPI.GetNexus();

            var org = nexus.GetOrganizationByName(nexus.RootStorage, name);
            var members = org.GetMembers();
            
            return new OrganizationResult()
            {
                id = org.ID,
                name = org.Name,
                members = members.Select(x => x.Text).ToArray(),
            };
        } 
        
        [APIInfo(typeof(OrganizationResult), "Returns info about all of organizations on chain.", false, 60)]
        [HttpGet("GetOrganizations")]
        public OrganizationResult[] GetOrganizations()
        {
            var nexus = NexusAPI.GetNexus();

            var orgs = nexus.GetOrganizations(nexus.RootStorage);

            return orgs.Select(x =>
            {
                var org = nexus.GetOrganizationByName(nexus.RootStorage, x);
                var members = org.GetMembers();

                return new OrganizationResult()
                {
                    id = org.ID,
                    name = x,
                    members = members.Select(y => y.Text).ToArray(),
                };
            }).ToArray();
        }
    }
}
