using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Interop;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.Pay.Chains;
using Serilog;

namespace Phantasma.Infrastructure.API.Controllers
{
    public class SwapController : BaseControllerV1
    {
        [APIInfo(typeof(string), "Tries to settle a pending swap for a specific hash.", false, 0, true)]
        [HttpGet("SettleCrossChainSwap")]
        public string SettleCrossChainSwap(string address, [APIParameter("Name of platform where swap transaction was created", "phantasma")] string sourcePlatform
                , [APIParameter("Name of platform to settle", "phantasma")] string destPlatform
                , [APIParameter("Hash of transaction to settle", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            var nexus = NexusAPI.GetNexus();
            Hash hash;
            if (!Hash.TryParse(hashText, out hash) || hash == Hash.Null)
            {
                throw new APIException("Invalid hash");
            }

            try
            {
                // Send a transaction to the interop contract the Validator Pays for it.
                // For the user to settle swap the transction.
                NexusAPI.SettleCrossChainSwap(Address.FromText(address), sourcePlatform, destPlatform, hash);
                return hashText;
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }
        
        [APIInfo(typeof(TokenSwapToSwap[]), "Returns all the platforms with all the swappers for a specific platform.", false, 0, true)]
        [HttpGet("GetSwappersForPlatform")]
        public TokenSwapToSwap[] GetSwappersForPlatform(string platform)
        {
            var nexus = NexusAPI.GetNexus();
            TokenSwapToSwap[] swappersPlatforms;
            swappersPlatforms = nexus.GetTokensSwapFromPlatform(platform, nexus.RootStorage);
            return swappersPlatforms;
        }
        
        [APIInfo(typeof(PlatformDetails), "Returns all the platforms with all the swappers for a specific platform.", false, 0, true)]
        [HttpGet("GetPlatformDetails")]
        public PlatformDetails GetPlatformDetails(string address, string platform)
        {
            if ( string.IsNullOrEmpty(address) )
            {
                throw new APIException("Invalid address");
            }
            
            if ( !Address.IsValidAddress(address) )
            {
                throw new APIException("Invalid address");
            }
            
            var nexus = NexusAPI.GetNexus();
            var platformDetails = nexus.RootChain
                .InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, NativeContractKind.Interop, nameof(InteropContract.GetPlatformDetailsForAddress), address, platform)
                .ToStruct<PlatformDetails>();
            return platformDetails;
        }

        [APIInfo(typeof(CrossChainTransfer[]), "Returns platform swaps for a specific address.", false, 0, true)]
        [HttpGet("GetSwapsForAddress")]
        public CrossChainTransfer[] GetSwapsForAddress([APIParameter("Address or account name", "helloman")] string account,
                string platform, bool extended = false)
        {
            var nexus = NexusAPI.GetNexus();

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
            
            var swaps = nexus.RootChain
                    .InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, NativeContractKind.Interop, nameof(InteropContract.GetCrossChainTransfersForUser), address)
                    .ToArray<CrossChainTransfer>();
            
            return swaps;
        }
    }
}
