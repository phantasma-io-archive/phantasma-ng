using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Tokens;
using Phantasma.Core;
using System.Numerics;

namespace Phantasma.Infrastructure.Controllers
{
    public class TokenController : BaseControllerV1
    {
        [APIInfo(typeof(TokenResult[]), "Returns an array of tokens deployed in Phantasma.", false, 300)]
        [HttpGet("GetTokens")]
        public TokenResult[] GetTokens(bool extended = false)
        {
            var nexus = NexusAPI.GetNexus();

            var tokenList = new List<TokenResult>();

            var symbols = nexus.GetTokens(nexus.RootStorage);
            foreach (var token in symbols)
            {
                var entry = NexusAPI.FillToken(token, false, extended);
                tokenList.Add(entry);
            }

            return tokenList.ToArray();
        }

        [APIInfo(typeof(TokenResult), "Returns info about a specific token deployed in Phantasma.", false, 120)]
        [HttpGet("GetToken")]
        public TokenResult GetToken([APIParameter("Token symbol to obtain info", "SOUL")] string symbol, bool extended = false)
        {
            var nexus = NexusAPI.GetNexus();

            if (!nexus.TokenExists(nexus.RootStorage, symbol))
            {
                throw new APIException("invalid token");
            }

            var result = NexusAPI.FillToken(symbol, true, extended);

            return result;
        }


        // deprecated
        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.", false, 15)]
        [HttpGet("GetTokenData")]
        public TokenDataResult GetTokenData([APIParameter("Symbol of token", "NACHO")] string symbol, [APIParameter("ID of token", "1")] string IDtext)
        {
            return GetNFT(symbol, IDtext, false);
        }


        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.", false, 15)]
        [HttpGet("GetNFT")]
        public TokenDataResult GetNFT([APIParameter("Symbol of token", "NACHO")] string symbol, [APIParameter("ID of token", "1")] string IDtext, bool extended = false)
        {
            var nexus = NexusAPI.GetNexus();

            if (!nexus.TokenExists(nexus.RootStorage, symbol))
            {
                throw new APIException("invalid token");
            }

            BigInteger ID;
            if (!BigInteger.TryParse(IDtext, out ID))
            {
                throw new APIException("invalid ID");
            }

            TokenDataResult result;
            try
            {
                result = NexusAPI.FillNFT(symbol, ID, extended);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            return result;
        }


        [APIInfo(typeof(TokenDataResult[]), "Returns an array of NFTs.", false, 300)]
        [HttpGet("GetNFTs")]
        public TokenDataResult[] GetNFTs([APIParameter("Symbol of token", "NACHO")] string symbol, [APIParameter("Multiple IDs of token, separated by comman", "1")] string IDText, bool extended = false)
        {
            var nexus = NexusAPI.GetNexus();

            if (!nexus.TokenExists(nexus.RootStorage, symbol))
            {
                throw new APIException("invalid token");
            }

            var IDs = IDText.Split(',');

            var list = new List<TokenDataResult>();

            try
            {
                foreach (var str in IDs)
                {
                    BigInteger ID;
                    if (!BigInteger.TryParse(str, out ID))
                    {
                        throw new APIException("invalid ID");
                    }

                    var result = NexusAPI.FillNFT(symbol, ID, extended);

                    list.Add(result);
                }
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            return list.ToArray();
        }


        [APIInfo(typeof(BalanceResult), "Returns the balance for a specific token and chain, given an address.", false, 5)]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("token is invalid", "-1")]
        [APIFailCase("chain is invalid", "-1re")]
        [HttpGet("GetTokenBalance")]
        public BalanceResult GetTokenBalance([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Token symbol", "SOUL")] string tokenSymbol, [APIParameter("Address or name of chain", "root")] string chainInput)
        {
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            var nexus = NexusAPI.GetNexus();

            if (!nexus.TokenExists(nexus.RootStorage, tokenSymbol))
            {
                throw new APIException("invalid token");
            }

            var tokenInfo = nexus.GetTokenInfo(nexus.RootStorage, tokenSymbol);

            var chain = NexusAPI.FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("invalid chain");
            }

            var address = Address.FromText(account);
            var token = nexus.GetTokenInfo(nexus.RootStorage, tokenSymbol);
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
