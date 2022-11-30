using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.Pay.Chains;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class SwapController : BaseControllerV1
    {
        [APIInfo(typeof(string), "Tries to settle a pending swap for a specific hash.", false, 0, true)]
        [HttpGet("SettleSwap")]
        public string SettleSwap([APIParameter("Name of platform where swap transaction was created", "phantasma")] string sourcePlatform
                , [APIParameter("Name of platform to settle", "phantasma")] string destPlatform
                , [APIParameter("Hash of transaction to settle", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            var nexus = NexusAPI.GetNexus();
            var tokenSwapper = NexusAPI.GetTokenSwapper();

            if (!tokenSwapper.SupportsSwap(sourcePlatform, destPlatform))
            {
                throw new APIException($"swaps between {sourcePlatform} and {destPlatform} not available");
            }

            if (!nexus.PlatformExists(nexus.RootStorage, sourcePlatform))
            {
                throw new APIException("Invalid source platform");
            }

            if (!nexus.PlatformExists(nexus.RootStorage, destPlatform))
            {
                throw new APIException("Invalid destination platform");
            }

            Hash hash;
            if (!Hash.TryParse(hashText, out hash) || hash == Hash.Null)
            {
                throw new APIException("Invalid hash");
            }

            if (destPlatform == DomainSettings.PlatformName)
            {
                try
                {
                    var swap = nexus.RootChain.GetSwap(nexus.RootStorage, hash);
                    if (swap.destinationHash != Hash.Null)
                    {
                        return swap.destinationHash.ToString();
                    }
                }
                catch
                {
                    // do nothing, just continue
                }
            }

            try
            {
                var destHash = tokenSwapper.SettleSwap(sourcePlatform, destPlatform, hash);

                if (destHash == Hash.Null)
                {
                    throw new APIException("Swap failed or destination hash is not yet available");
                }
                else
                {
                    return destHash.ToString();
                }
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }

        [APIInfo(typeof(SwapResult[]), "Returns platform swaps for a specific address.", false, 0, true)]
        [HttpGet("GetSwapsForAddress")]
        public SwapResult[] GetSwapsForAddress([APIParameter("Address or account name", "helloman")] string account,
                string platform, bool extended = false)
        {
            var nexus = NexusAPI.GetNexus();
            var tokenSwapper = NexusAPI.GetTokenSwapper();

            Address address;

            switch (platform)
            {
                case DomainSettings.PlatformName:
                    address = Address.FromText(account);
                    break;
                case NeoWallet.NeoPlatform:
                    address = NeoWallet.EncodeAddress(account);
                    break;
                case EthereumWallet.EthereumPlatform:
                    address = EthereumWallet.EncodeAddress(account);
                    break;
                case Pay.Chains.BSCWallet.BSCPlatform:
                    address = Pay.Chains.BSCWallet.EncodeAddress(account);
                    break;
                default:
                    address = nexus.LookUpName(nexus.RootStorage, account, Timestamp.Now);
                    break;
            }

            if (address.IsNull)
            {
                throw new APIException("invalid address");
            }

            var swapList = tokenSwapper.GetPendingSwaps(address);

            var oracleReader = nexus.GetOracleReader();

            var txswaps = swapList.
                Select(x => new KeyValuePair<ChainSwap, InteropTransaction>(x, oracleReader.ReadTransaction(x.sourcePlatform, x.sourceChain, x.sourceHash))).ToArray();

            var swaps = txswaps.Where(x => x.Value != null && x.Value.Transfers.Length > 0).
                Select(x => new SwapResult()
                {
                    sourcePlatform = x.Key.sourcePlatform,
                    sourceChain = x.Key.sourceChain,
                    sourceHash = x.Key.sourceHash.ToString(),
                    destinationPlatform = x.Key.destinationPlatform,
                    destinationChain = x.Key.destinationChain,
                    destinationHash = x.Key.destinationHash == Hash.Null ? "pending" : x.Key.destinationHash.ToString(),
                    sourceAddress = x.Value.Transfers[0].sourceAddress.Text,
                    destinationAddress = x.Value.Transfers[0].destinationAddress.Text,
                    symbol = x.Value.Transfers[0].Symbol,
                    value = x.Value.Transfers[0].Value.ToString(),
                });

            if (extended)
            {
                var oldSwaps = (InteropHistory[])nexus.RootChain.InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, "interop", nameof(InteropContract.GetSwapsForAddress), address).ToObject();

                swaps = swaps.Concat(oldSwaps.Select(x => new SwapResult()
                {
                    sourcePlatform = x.sourcePlatform,
                    sourceChain = x.sourceChain,
                    sourceHash = x.sourceHash.ToString(),
                    destinationPlatform = x.destPlatform,
                    destinationChain = x.destChain,
                    destinationHash = x.destHash.ToString(),
                    sourceAddress = x.sourceAddress.Text,
                    destinationAddress = x.destAddress.Text,
                    symbol = x.symbol,
                    value = x.value.ToString(),
                }));
            }

            return swaps.ToArray();
        }
    }
}
