using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Infrastructure.API.Interfaces;
using Phantasma.Infrastructure.API.Structs;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class OrganizationController : BaseControllerV1
    {
        [APIInfo(typeof(OrganizationResult), "Returns info about an organization.", false, 60)]
        [HttpGet("GetOrganization")]
        public OrganizationResult GetOrganization(
            [APIParameter(description: "ID of the organization to look up", value: "validators")]
            string id,
            [APIParameter(
                description: "Extended data includes members. True by default. (will be set to false in the future)",
                value: "true")]
            bool extended = true)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (string.IsNullOrEmpty(id))
            {
                throw new APIException("invalid organization ID");
            }

            if (!service.OrganizationExists(id))
            {
                throw new APIException("invalid organization");
            }

            return CreateOrganizationResult(id, service, extended);
        }

        [APIInfo(typeof(OrganizationResult), "Returns info about an organization.", false, 60)]
        [HttpGet("GetOrganizationByName")]
        public OrganizationResult GetOrganizationByName(
            [APIParameter(description: "Name of the organization to look up", value: "Block Producers")]
            string name,
            [APIParameter(
                description: "Extended data includes members. True by default. (will be set to false in the future)",
                value: "true")]
            bool extended = true)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (string.IsNullOrEmpty(name))
            {
                throw new APIException("invalid organization name");
            }

            return CreateOrganizationResult(name, service, extended);
        }

        [APIInfo(typeof(OrganizationResult), "Returns info about all of organizations on chain.", false, 60)]
        [HttpGet("GetOrganizations")]
        public OrganizationResult[] GetOrganizations(
            [APIParameter(description: "Extended data includes members. True by default. (will be set to false in the future)",
                value: "false")]
            bool extended = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetOrganizations()
                .Select(name => CreateOrganizationResult(name, service, extended))
                .ToArray();
        }

        private OrganizationResult CreateOrganizationResult(string name, IAPIService service, bool extended)
        {
            var org = service.GetOrganizationByName(name);
            return new OrganizationResult
            {
                id = org.ID,
                name = name,
                members = GetMembers(extended, org),
            };
        }

        private static string[] GetMembers(bool extended, IOrganization org)
        {
            return extended
                ? org.GetMembers().Select(member => member.Text).ToArray()
                : Array.Empty<string>();
        }
    }
}
