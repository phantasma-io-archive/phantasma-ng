using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.API.Interfaces;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class AccountController : BaseControllerV1
    {
        private readonly Dictionary<string, List<AccountResult>> _cacheAddresses =
            new Dictionary<string, List<AccountResult>>();

        private readonly Dictionary<string, uint> _cacheTimers = new Dictionary<string, uint>();
        private static readonly uint CachedTime = (uint)TimeSpan.FromDays(1).TotalSeconds;

        // extended parameter added during the transition towards removing transaction details from GetAccount
        [APIInfo(typeof(AccountResult), "Returns the account name and balance of given address.", false, 10)]
        [APIFailCase("address is invalid", "ABCD123")]
        [HttpGet("GetAccount")]
        public AccountResult GetAccount(
            [APIParameter("Address of account", "P2KJC8TB6XyY9sr1s4GprPD2XjnnUggRq86pwsFZjUEqCXt")]
            string account,
            [APIParameter(
                description:
                "Extended data. Returns all transactions. (deprecated, will be removed in future versions)",
                value: "true")]
            bool extended = true)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            var address = Address.FromText(account);
            AccountResult result;

            try
            {
                result = service.FillAccount(address, extended);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw new APIException(e.Message);
            }

            return result;
        }

        // extended parameter added during the transition towards removing transaction details from GetAccounts
        [APIInfo(typeof(AccountResult[]), "Returns data about several accounts.", false, 10)]
        [HttpGet("GetAccounts")]
        public AccountResult[] GetAccounts(
            [APIParameter("Comma-delimited list of accounts",
                "P2KJC8TB6XyY9sr1s4GprPD2XjnnUggRq86pwsFZjUEqCXt, P2KBh1rkUZXNj7i1SfegUEg26YfrcYDRBt69fj6izVAPbtc")]
            string accounts,
            [APIParameter(
                description:
                "Extended data. Returns all transactions. (deprecated, will be removed in future versions)",
                value: "true")]
            bool extended = true)
        {
            var serviceToUse = HttpContext.Items["APIService"] as IAPIService;
            var accountsArray = accounts.Split(',');
            
            return accountsArray.Select(account => GetAccountInternal(account, extended, serviceToUse)).ToArray();
        }

        [APIInfo(typeof(string), "Returns the address that owns a given name.", false, 60)]
        [APIFailCase("address is invalid", "ABCD123")]
        [HttpGet("LookUpName")]
        public string LookUpName([APIParameter("Name of account", "beyondtheneon")] string name)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (!ValidationUtils.IsValidIdentifier(name))
            {
                throw new APIException("invalid name");
            }
            
            var address = service.LookUpName(name);
            if (address.IsNull)
            {
                throw new APIException("name not owned");
            }

            return address.Text;
        }

        // extended parameter added during the transition towards removing transaction details from GetAddressesBySymbol
        [APIInfo(typeof(AccountResult[]), "Returns data about several accounts.", false, 8600)]
        [HttpGet("GetAddressesBySymbol")]
        public AccountResult[] GetAddressesBySymbol(
            [APIParameter("Token symbol", "SOUL")] string symbol,
            [APIParameter(
                description:
                "Extended data. Returns all transactions. (deprecated, will be removed in future versions)",
                value: "true")]
            bool extended = true)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (string.IsNullOrEmpty(symbol))
                throw new APIException("invalid symbol");

            if (IsCached(symbol, out var cachedResults))
                return cachedResults.ToArray();

            var accounts = service.GetAddressesBySymbol(symbol);

            var results = accounts.Select(account =>
            {
                var address = ValidateAndGetAddress(account);
                var result = extended
                    ? service.FillAccount(address, true)
                    : new AccountResult { address = address.Text };

                return result;
            }).ToList();

            UpdateCache(symbol, results);
            return results.ToArray();
        }

        private AccountResult GetAccountInternal(string account, bool extended, IAPIService serviceToUse)
        {
            var address = ValidateAndGetAddress(account);
            return serviceToUse.FillAccount(address, extended);
        }

        private Address ValidateAndGetAddress(string account)
        {
            if (!Address.IsValidAddress(account))
                throw new APIException("invalid address");

            return Address.FromText(account);
        }

        private bool IsCached(string symbol, out List<AccountResult> cachedResults)
        {
            cachedResults = null;

            if (!_cacheAddresses.ContainsKey(symbol)) return false;
            cachedResults = _cacheAddresses[symbol];
            return _cacheTimers[symbol] + CachedTime > Timestamp.Now.Value;
        }

        private void UpdateCache(string symbol, List<AccountResult> results)
        {
            _cacheAddresses[symbol] = results;
            _cacheTimers[symbol] = Timestamp.Now.Value;
            Console.WriteLine("Added to cache"); // Consider using a structured logger instead
        }
    }
}
