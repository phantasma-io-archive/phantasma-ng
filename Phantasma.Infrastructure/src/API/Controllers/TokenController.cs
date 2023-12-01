using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Blockchain.Tokens.Structs;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Infrastructure.API.Structs;
using Phantasma.Infrastructure.Utilities;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class TokenController : BaseControllerV1
    {
        [APIInfo(typeof(TokenResult[]), "Returns an array of tokens deployed in Phantasma.", false, 300)]
        [HttpGet("GetTokens")]
        public TokenResult[] GetTokens(
            [APIParameter(
                description:
                "Extended data. Returns scripts, methods, and prices. (deprecated, will be removed in future API versions)",
                value: "false")]
            bool extended = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);

            var symbols = service.GetAvailableTokenSymbols();

            return symbols.Select(token => service.FillToken(token, false, extended)).ToArray();
        }

        [APIInfo(typeof(TokenResult), "Returns info about a specific token deployed in Phantasma.", false, 120)]
        [HttpGet("GetToken")]
        public TokenResult GetToken([APIParameter("Token symbol to obtain info", "SOUL")] string symbol,
            [APIParameter(
                description:
                "Extended data. Returns script, methods, and prices. (prices will be removed in future API versions)",
                value: "false")]
            bool extended)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (!service.TokenExists(symbol))
            {
                throw new APIException("invalid token");
            }

            var result = service.FillToken(symbol, true, extended);

            return result;
        }

        // deprecated
        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.", false, 15)]
        [HttpGet("GetTokenData")]
        public TokenDataResult GetTokenData([APIParameter("Symbol of token", "NACHO")] string symbol,
            [APIParameter("ID of token", "1")] string IDtext)
        {
            return GetNFT(symbol, IDtext, false);
        }


        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.", false, 15)]
        [HttpGet("GetNFT")]
        public TokenDataResult GetNFT([APIParameter("Symbol of token", "NACHO")] string symbol,
            [APIParameter("ID of token", "1")] string IDtext,
            [APIParameter(description: "Extended data. Returns properties.", value: "false")]
            bool extended = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (!service.TokenExists(symbol))
            {
                throw new APIException("invalid token");
            }

            if (!BigInteger.TryParse(IDtext, out var ID))
            {
                throw new APIException("invalid ID");
            }

            TokenDataResult result;
            try
            {
                result = service.FillNFT(symbol, ID, extended);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            return result;
        }


        [APIInfo(typeof(TokenDataResult[]), "Returns an array of NFTs.", false, 300)]
        [HttpGet("GetNFTs")]
        public TokenDataResult[] GetNFTs([APIParameter("Symbol of token", "NACHO")] string symbol,
            [APIParameter("Multiple IDs of token, separated by comma", "1")]
            string IDText,
            [APIParameter(description: "Extended data. Returns properties.", value: "false")]
            bool extended = false)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (!service.TokenExists(symbol))
            {
                throw new APIException("invalid token");
            }

            var ds = IDText.Split(',');

            var list = new List<TokenDataResult>();

            try
            {
                foreach (var str in ds)
                {
                    BigInteger ID;
                    if (!BigInteger.TryParse(str, out ID))
                    {
                        throw new APIException("invalid ID");
                    }

                    var result = service.FillNFT(symbol, ID, extended);

                    list.Add(result);
                }
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            return list.ToArray();
        }


        [APIInfo(typeof(BalanceResult), "Returns the balance for a specific token and chain, given an address.", false,
            5)]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("token is invalid", "-1")]
        [APIFailCase("chain is invalid", "-1re")]
        [HttpGet("GetTokenBalance")]
        public BalanceResult GetTokenBalance(
            [APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")]
            string account,
            [APIParameter("Token symbol", "SOUL")] string tokenSymbol,
            [APIParameter("Address or name of chain", "root")]
            string chainInput)
        {
            var service = ServiceUtility.GetAPIService(HttpContext);
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            if (!service.TokenExists(tokenSymbol))
            {
                throw new APIException("invalid token");
            }

            var tokenInfo = service.GetTokenInfo(tokenSymbol);

            var chain = service.FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("invalid chain");
            }

            var address = Address.FromText(account);
            var token = service.GetTokenInfo(tokenSymbol);
            var balance = chain.GetTokenBalance(chain.Storage, token, address);

            var result = new BalanceResult()
            {
                amount = balance.ToString(),
                symbol = tokenSymbol,
                decimals = (uint)tokenInfo.Decimals,
                chain = chain.Address.Text
            };

            if (!tokenInfo.IsFungible())
            {
                var ownerships = new OwnershipSheet(tokenSymbol);
                var idList = ownerships.Get(chain.Storage, address);
                if (idList != null && idList.Any())
                {
                    result.ids = idList.Select(x => x.ToString()).ToArray();
                }
            }

            return result;
        }
    }
}
