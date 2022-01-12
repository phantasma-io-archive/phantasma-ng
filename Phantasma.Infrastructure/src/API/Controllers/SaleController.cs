using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Contracts;
using Phantasma.Core;

namespace Phantasma.Infrastructure.Controllers
{
    public class SaleController : BaseControllerV1
    {
        [APIInfo(typeof(string), "Returns latest sale hash.", false, -1)]
        [HttpGet("GetLatestSaleHash")]
        public string GetLatestSaleHash()
        {
            var nexus = NexusAPI.GetNexus();

            var hash = (Hash)nexus.RootChain.InvokeContract(nexus.RootChain.Storage, "sale", nameof(SaleContract.GetLatestSaleHash)).ToObject();

            return hash.ToString();
        }

        [APIInfo(typeof(CrowdsaleResult), "Returns data about a crowdsale.", false, -1)]
        [APIFailCase("hash is invalid", "43242342")]
        [HttpGet("GetSale")]
        public CrowdsaleResult GetSale([APIParameter("Hash of sale", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;
            if (!Hash.TryParse(hashText, out hash) || hash == Hash.Null)
            {
                throw new APIException("Invalid hash");
            }

            var nexus = NexusAPI.GetNexus();

            var sale = (SaleInfo)nexus.RootChain.InvokeContract(nexus.RootChain.Storage, "sale", nameof(SaleContract.GetSale), hash).ToObject();

            return new CrowdsaleResult()
            {
                hash = hashText,
                name = sale.Name,
                creator = sale.Creator.Text,
                flags = sale.Flags.ToString(),
                startDate = sale.StartDate.Value,
                endDate = sale.EndDate.Value,
                sellSymbol = sale.SellSymbol,
                receiveSymbol = sale.ReceiveSymbol,
                price = (uint)sale.Price,
                globalSoftCap = sale.GlobalSoftCap.ToString(),
                globalHardCap = sale.GlobalHardCap.ToString(),
                userSoftCap = sale.UserSoftCap.ToString(),
                userHardCap = sale.UserHardCap.ToString(),
            };
        }
    }
}
