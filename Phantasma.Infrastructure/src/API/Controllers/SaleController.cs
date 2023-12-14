using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Infrastructure.API.Structs;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class SaleController : BaseControllerV1
    {
        [APIInfo(typeof(string), "Returns latest sale hash.", false, -1)]
        [HttpGet("GetLatestSaleHash")]
        public string GetLatestSaleHash()
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            return service.GetLatestSaleHash();
        }

        [APIInfo(typeof(CrowdsaleResult), "Returns data about a crowdsale.", false, -1)]
        [APIFailCase("hash is invalid", "43242342")]
        [HttpGet("GetSale")]
        public CrowdsaleResult GetSale(
            [APIParameter("Hash of sale", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")]
            string hashText)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (!Hash.TryParse(hashText, out var hash) || hash == Hash.Null)
            {
                throw new APIException("Invalid hash");
            }

            var sale = service.GetSale(hash);

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
