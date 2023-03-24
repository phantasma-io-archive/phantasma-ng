using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class AuctionController : BaseControllerV1
    {
        [APIInfo(typeof(int), "Returns the number of active auctions.", false, 30)]
        [HttpGet("GetAuctionsCount")]
        public int GetAuctionsCount([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName = null, [APIParameter("Token symbol used as filter", "NACHO")]
            string symbol = null)
        {
            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
            {
                throw new APIException("Market not available");
            }

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContractAtTimestamp(chain.Storage, Timestamp.Now, "market", "GetAuctions").ToObject();

            if (!string.IsNullOrEmpty(symbol))
            {
                entries = entries.Where(x => x.BaseSymbol == symbol);
            }

            return entries.Count();
        }

        [APIInfo(typeof(AuctionResult[]), "Returns the auctions available in the market.", true, 30)]
        [HttpGet("GetAuctions")]
        public PaginatedResult GetAuctions([APIParameter("Chain address or name where the market is located", "NACHO")] string chainAddressOrName, [APIParameter("Token symbol used as filter", "NACHO")] string symbol = null,
            [APIParameter("Index of page to return", "5")] uint page = 1,
            [APIParameter("Number of items to return per page", "5")] uint pageSize = NexusAPI.PaginationMaxResults)
        {
            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
            {
                throw new APIException("Market not available");
            }

            if (page < 1 || pageSize < 1)
            {
                throw new APIException("invalid page/pageSize");
            }

            if (pageSize > NexusAPI.PaginationMaxResults)
            {
                pageSize = NexusAPI.PaginationMaxResults;
            }

            var paginatedResult = new PaginatedResult();

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContractAtTimestamp(chain.Storage, Timestamp.Now, "market", "GetAuctions").ToObject();

            if (!string.IsNullOrEmpty(symbol))
            {
                entries = entries.Where(x => x.BaseSymbol == symbol);
            }

            // pagination
            uint numberRecords = (uint)entries.Count();
            uint totalPages = (uint)Math.Ceiling(numberRecords / (double)pageSize);
            //

            entries = entries.OrderByDescending(p => p.StartDate.Value)
                .Skip((int)((page - 1) * pageSize))
                .Take((int)pageSize);

            paginatedResult.pageSize = pageSize;
            paginatedResult.totalPages = totalPages;
            paginatedResult.total = numberRecords;
            paginatedResult.page = page;

            paginatedResult.result = entries.Select(x => NexusAPI.FillAuction(x, chain)).ToArray();

            return paginatedResult;
        }

        [APIInfo(typeof(AuctionResult), "Returns the auction for a specific token.", false, 30)]
        [HttpGet("GetAuction")]
        public AuctionResult GetAuction([APIParameter("Chain address or name where the market is located", "NACHO")] string chainAddressOrName, [APIParameter("Token symbol", "NACHO")] string symbol, [APIParameter("Token ID", "1")] string IDtext)
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

            try
            {
                var info = nexus.ReadNFT(nexus.RootStorage, symbol, ID);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            var chain = NexusAPI.FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
            {
                throw new APIException("Market not available");
            }

            var nft = nexus.ReadNFT(nexus.RootStorage, symbol, ID);

            var forSale = chain.InvokeContractAtTimestamp(chain.Storage, Timestamp.Now, "market", "HasAuction", symbol, ID).AsBool();
            if (!forSale)
            {
                throw new APIException("Token not for sale");
            }

            var auction = (MarketAuction)chain.InvokeContractAtTimestamp(chain.Storage, Timestamp.Now, "market", "GetAuction", symbol, ID).ToObject();

            return new AuctionResult()
            {
                baseSymbol = auction.BaseSymbol,
                quoteSymbol = auction.QuoteSymbol,
                tokenId = auction.TokenID.ToString(),
                creatorAddress = auction.Creator.Text,
                chainAddress = chain.Address.Text,
                price = auction.Price.ToString(),
                endPrice = auction.EndPrice.ToString(),
                extensionPeriod = auction.ExtensionPeriod.ToString(),
                startDate = auction.StartDate.Value,
                endDate = auction.EndDate.Value,
                ram = Base16.Encode(nft.RAM),
                rom = Base16.Encode(nft.ROM),
                type = auction.Type.ToString(),
                listingFee = auction.ListingFee.ToString(),
                currentWinner = auction.CurrentBidWinner == Address.Null ? "" : auction.CurrentBidWinner.Text
            };
        }
    }
}
