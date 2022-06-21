using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core;

namespace Phantasma.Infrastructure.Controllers
{
    public class AccountController : BaseControllerV1
    {
        [APIInfo(typeof(AccountResult), "Returns the account name and balance of given address.", false, 10)]
        [APIFailCase("address is invalid", "ABCD123")]
        [HttpGet("GetAccount")]
        public async Task<AccountResult> GetAccount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account)
        {
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            var address = Address.FromText(account);

            AccountResult result;

            try
            {
                result = await NexusAPI.FillAccount(address);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            return result;
        }

        [APIInfo(typeof(AccountResult[]), "Returns data about several accounts.", false, 10)]
        [HttpGet("GetAccounts")]
        public async Task<AccountResult[]> GetAccounts([APIParameter("Multiple addresses separated by comma", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV,PDHcFHq2femFnj2ZaFc7iu3qD4XjZG9eVZXuwDrtJGDhj")] string accountText)
        {
            var accounts = accountText.Split(',');

            var list = new List<AccountResult>();

            foreach (var account in accounts)
            {
                if (!Address.IsValidAddress(account))
                {
                    throw new APIException("invalid address");
                }

                var address = Address.FromText(account);

                AccountResult result;

                try
                {
                    result = await NexusAPI.FillAccount(address);
                    list.Add(result);
                }
                catch (Exception e)
                {
                    throw new APIException(e.Message);
                }
            }

            return list.ToArray();
        }

        [APIInfo(typeof(string), "Returns the address that owns a given name.", false, 60)]
        [APIFailCase("address is invalid", "ABCD123")]
        [HttpGet("LookUpName")]
        public async Task<string> LookUpName([APIParameter("Name of account", "blabla")] string name)
        {
            if (!ValidationUtils.IsValidIdentifier(name))
            {
                throw new APIException("invalid name");
            }

            var nexus = NexusAPI.GetNexus();

            var address = await nexus.LookUpName(nexus.RootStorage, name);
            if (address.IsNull)
            {
                throw new APIException("name not owned");
            }

            return address.Text;
        }
    }
}
