using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Nethereum.Util;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using UnitConversion = Phantasma.Core.Numerics.UnitConversion;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class InteropContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Interop;
        private static BigInteger _minimumFeePercentage = 0;
        private static BigInteger _maximumFeePercentage = 25;
        private const decimal _maximumTransferAmount = 50000;
        private const uint _bettingDuration = 45; // 45 seconds

#pragma warning disable 0649
        private StorageMap _platformHashes;
        private StorageList _withdraws;
        private StorageMap _platformsAddresses; // <Address, PlatformDetails>
        private StorageMap _platformsSwaps; // <Address, StorageList<InteropHistory>>
        private StorageMap _swapperTransactions; // <Address, StorageList<InteropHistory>>
        private StorageMap _crossChainTransfers; // <Address, StorageList<CrossChainTransfer>>
        private StorageMap _crossChainUserTransfers; // <Address, StorageList<CrossChainTransferHistory>>
        private StorageMap _PlatformSwappers; // <string, StorageList<TokenToSwap>>
        private StorageMap _crossChainBetting; // <CrossChainTransfer, StorageList<Bet>>
        private StorageMap _crossChainBetWinners; // <CrossChainTransfer, Address>

        internal StorageMap _swapMap; //<Hash, Collection<InteropHistory>>
        internal StorageMap _historyMap; //<Address, Collection<Hash>>
#pragma warning restore 0649

        public InteropContract() : base()
        {
        }


        // This contract call associates a new swap address to a specific platform. 
        // Existing swap addresses will still be considered valid for receiving funds
        // However nodes should start sending assets from this new address when doing swaps going from Phantasma to this platform
        // For all purposes, any transfer coming from another swap address of same platform into this one shall not being considered a "swap"        
        public void RegisterAddress(Address from, string platform, Address localAddress, string externalAddress)
        {
            Runtime.Expect(Runtime.ProtocolVersion < 13, "this method is obsolete");
            Runtime.Expect(false, "not allowed for now");
            //Runtime.Expect(from == Runtime.GenesisAddress, "only genesis allowed");
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(localAddress.IsInterop, "swap target must be interop address");

            Runtime.RegisterPlatformAddress(platform, localAddress, externalAddress);
        }

        #region New Swapper Way
        /// <summary>
        /// Register platform swappers
        /// </summary>
        /// <param name="platform"></param>
        private void RegisterPlatformSwappers(string platform)
        {
            if (_PlatformSwappers.ContainsKey(platform.ToLower()))
            {
                // Already registered.
                return;
            }

            _PlatformSwappers.Set(platform.ToLower(), new StorageList());
        }

        /// <summary>
        /// Method used to get the list of platform swappers.
        /// </summary>
        /// <param name="platform"></param>
        /// <returns></returns>
        private StorageList GetPlatformSwappers(string platform)
        {
            if (!_PlatformSwappers.ContainsKey(platform.ToLower()))
            {
                RegisterPlatformSwappers(platform.ToLower());
            }

            return _PlatformSwappers.Get<string, StorageList>(platform.ToLower());
        }

        /// <summary>
        /// Method used to Set the Platform Swapper
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="externalContractAddress"></param>
        /// <param name="swapper"></param>
        private void SetPlatformSwapper(string platform, string externalContractAddress, TokenSwapToSwap swapper)
        {
            var swappersList = GetPlatformSwappers(platform);
            if (swappersList.Count() == 0)
            {
                swappersList.Add<TokenSwapToSwap>(swapper);
                _PlatformSwappers.Set(platform, swappersList);
                return;
            }

            var swappers = swappersList.All<TokenSwapToSwap>();
            for (int i = 0; i < swappers.Length; i++)
            {
                if (swappers[i].Symbol == swapper.Symbol &&
                    swappers[i].ExternalContractAddress == swapper.ExternalContractAddress)
                {
                    // Already registered.
                    swappersList.Replace(i, swapper);
                    _PlatformSwappers.Set(platform, swappersList);
                    return;
                }
            }

            swappersList.Add<TokenSwapToSwap>(swapper);
            _PlatformSwappers.Set(platform, swappersList);
        }

        /// <summary>
        /// Method used to add a swapper to the list.
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="symbol"></param>
        /// <param name="decimals"></param>
        /// <param name="externalContractAddress"></param>
        /// <param name="InternalAddress"></param>
        /// <param name="externalAddress"></param>
        /// <param name="isActive"></param>
        private void AddSwapper(string platform, string symbol, int decimals, string externalContractAddress,
            Address InternalAddress, string externalAddress, bool isActive)
        {
            var swappersList = GetPlatformSwappers(platform);
            var tokenSwappers = swappersList.All<TokenSwapToSwap>();
            Swapper swapper = new Swapper(InternalAddress, externalAddress, isActive);
            for (int i = 0; i < tokenSwappers.Length; i++)
            {
                if (tokenSwappers[i].Symbol == symbol &&
                    tokenSwappers[i].ExternalContractAddress == externalContractAddress)
                {
                    var hasSwapper = tokenSwappers[i].Swappers.Select(_swapper =>
                            _swapper.ExternalAddress == externalAddress && _swapper.InternalAddress == InternalAddress)
                        .Any();
                    if (!hasSwapper)
                    {
                        tokenSwappers[i].Swappers.ToList().Add(swapper);
                        SetPlatformSwapper(platform, externalContractAddress, tokenSwappers[i]);
                    }

                    return;
                }
            }

            TokenSwapToSwap tokenSwapToSwap = new TokenSwapToSwap(platform, symbol, decimals, externalContractAddress,
                new Swapper[] { swapper });
            SetPlatformSwapper(platform, externalContractAddress, tokenSwapToSwap);
        }

        /// <summary>
        /// Method used to update a swapper.
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="symbol"></param>
        /// <param name="externalContractAddress"></param>
        /// <param name="InternalAddress"></param>
        /// <param name="externalAddress"></param>
        /// <param name="isActive"></param>
        private void UpdateSwapper(string platform, string symbol, string externalContractAddress,
            Address InternalAddress, string externalAddress, bool isActive)
        {
            var platformTokensList = GetPlatformSwappers(platform);
            var tokenSwappers = platformTokensList.All<TokenSwapToSwap>();
            for (int i = 0; i < tokenSwappers.Length; i++)
            {
                if (tokenSwappers[i].Symbol == symbol &&
                    tokenSwappers[i].ExternalContractAddress == externalContractAddress)
                {
                    var swappersList = tokenSwappers[i].Swappers.ToList();
                    var hasSwapper = swappersList.Select(_swapper =>
                            _swapper.ExternalAddress == externalAddress && _swapper.InternalAddress == InternalAddress)
                        .Any();
                    if (hasSwapper)
                    {
                        if (!isActive)
                        {
                            // Remove
                            var swapper = swappersList.First(_swapper =>
                                _swapper.ExternalAddress == externalAddress &&
                                _swapper.InternalAddress == InternalAddress);
                            swappersList.Remove(swapper);
                            tokenSwappers[i].Swappers = swappersList.ToArray();
                        }

                        SetPlatformSwapper(platform, externalContractAddress, tokenSwappers[i]);
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// Register a platform for a specific address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <param name="localAddress"></param>
        /// <param name="externalAddress"></param>
        /// <param name="platformName"></param>
        /// <param name="mainSymbol"></param>
        /// <param name="fuelSymbol"></param>
        /// <param name="decimals"></param>
        public void RegisterPlatformForAddress(Address from, string platform, Address localAddress,
            string externalAddress, string mainSymbol, string fuelSymbol, int decimals, bool isSwapEnabled,
            BigInteger feePercentage)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(localAddress.IsInterop, "swap target must be interop address");
            Runtime.Expect(Nexus.IsDangerousAddress(from), "invalid target address");

            Runtime.Expect(!HasPlatformInfo(from, platform), "platform already registered");
            Runtime.Expect(IsValidPlatform(platform), "invalid platform");
            Runtime.Expect(feePercentage >= _minimumFeePercentage,
                $"Low fee percentage, needs to be greater or equal to {_minimumFeePercentage}");
            Runtime.Expect(feePercentage <= _maximumFeePercentage,
                $"High fee percentage, needs to be lower or equal to {_maximumFeePercentage}");

            Runtime.Expect(Runtime.TokenExists(mainSymbol), "invalid main symbol");
            Runtime.Expect(Runtime.TokenExists(fuelSymbol), "invalid fuel symbol");


            var platformsForAddress = GetPlatformsForAddress(from);
            if (platformsForAddress == null)
            {
                platformsForAddress = new PlatformDetails[0];
            }

            var validators = Runtime.GetValidators();
            bool isMainSwapper = validators.First(v => v.address == from).address == from;

            var platformInfoDetails = new PlatformDetails()
            {
                Name = platform,
                MainSymbol = mainSymbol,
                FuelSymbol = fuelSymbol,
                Decimals = decimals,
                Owner = from,
                ExternalAddress = externalAddress,
                LocalAddress = localAddress,
                IsSwapEnabled = isSwapEnabled,
                Tokens = new PlatformTokens[0],
                MainSwapper = isMainSwapper,
                FeePercentage = feePercentage
            };

            var tempPlatfroms = platformsForAddress.ToList();
            tempPlatfroms.Add(platformInfoDetails);
            platformsForAddress = tempPlatfroms.ToArray();

            _platformsAddresses.Set(from, platformsForAddress);

            Runtime.RegisterPlatformAddress(platform, localAddress, externalAddress);

            // Emit event
        }

        /// <summary>
        /// Update Platform Details
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <param name="localAddress"></param>
        /// <param name="externalAddress"></param>
        /// <param name="mainSymbol"></param>
        /// <param name="fuelSymbol"></param>
        /// <param name="decimals"></param>
        /// <param name="isSwapEnabled"></param>
        public void UpdatePlatformDetails(Address from, string platform, Address localAddress,
            string externalAddress, string mainSymbol, string fuelSymbol, int decimals, bool isSwapEnabled,
            BigInteger feePercentage)
        {
            Runtime.Expect(from.IsUser, "Needs to be a user address");
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(HasPlatformInfo(from, platform), "platform not registered");
            Runtime.Expect(Nexus.IsDangerousAddress(from), "invalid target address");

            Runtime.Expect(feePercentage >= _minimumFeePercentage,
                $"Low fee percentage, needs to be greater or equal to {_minimumFeePercentage}");
            Runtime.Expect(feePercentage <= _maximumFeePercentage,
                $"High fee percentage, needs to be lower or equal to {_maximumFeePercentage}");

            Runtime.Expect(Runtime.TokenExists(mainSymbol), "invalid main symbol");
            Runtime.Expect(Runtime.TokenExists(fuelSymbol), "invalid fuel symbol");

            var validators = Runtime.GetValidators();
            bool isMainSwapper = validators.First(v => v.address == from).address == from;

            var platformDetails = GetPlatformDetailsForAddress(from, platform);
            var platformsForAddress = GetPlatformsForAddress(from);
            var platformDetailsUpdated = new PlatformDetails()
            {
                Name = platform,
                MainSymbol = mainSymbol,
                FuelSymbol = fuelSymbol,
                Decimals = decimals,
                Owner = from,
                ExternalAddress = externalAddress,
                LocalAddress = localAddress,
                IsSwapEnabled = isSwapEnabled,
                Tokens = platformDetails.Tokens,
                MainSwapper = isMainSwapper,
                FeePercentage = feePercentage
            };

            var tempPlatfroms = platformsForAddress.ToList();
            var platformInfoIndex = platformsForAddress.ToList().FindIndex(p => p.Name == platform);
            tempPlatfroms[platformInfoIndex] = platformDetailsUpdated;
            platformsForAddress = tempPlatfroms.ToArray();

            _platformsAddresses.Set(from, platformsForAddress);
        }

        /// <summary>
        /// Register Token on platform for a specific address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <param name="symbol"></param>
        /// <param name="decimals"></param>
        public void RegisterTokenOnPlatform(Address from, string platform, string symbol, int decimals,
            string externalContractAddress, Address localContractAddress, Address localAddress, string externalAddress)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(from.IsUser, "Needs to be a user address");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");
            Runtime.Expect(Runtime.TokenExists(symbol), "token not registered");
            Runtime.Expect(Nexus.IsDangerousAddress(from), "invalid target address");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(tokenInfo.IsTransferable(), "token not transferable");
            Runtime.Expect(tokenInfo.IsFungible(), "token not fungible");

            var platformsForAddress = GetPlatformsForAddress(from);
            var platformInfoIndex = platformsForAddress.ToList().FindIndex(p => p.Name == platform);
            var platformInfo = platformsForAddress.FirstOrDefault(p => p.Name == platform);
            Runtime.Expect(platformInfo.Name == platform, "platform not registered");

            var tokens = platformInfo.Tokens.ToList();
            var token = new PlatformTokens()
            {
                Symbol = symbol,
                Decimals = decimals,
                ExternalContractAddress = externalContractAddress,
                LocalContractAddress = localContractAddress,
                LocalAddress = localAddress,
                ExternalAddress = externalAddress
            };

            tokens.Add(token);
            platformInfo.Tokens = tokens.ToArray();

            platformsForAddress[platformInfoIndex] = platformInfo;
            _platformsAddresses.Set(from, platformsForAddress);

            AddSwapper(platform, symbol, decimals, externalContractAddress, localAddress, externalAddress,
                platformInfo.IsSwapEnabled);

            // Emit event
            //Runtime.Notify(EventKind.Custom, from, new TokenEventData(DomainSettings.StakingTokenSymbol, stakeAmount, Runtime.Chain.Name));
        }

        /// <summary>
        /// Remove Tokens from the platform for a specific address.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <param name="symbol"></param>
        public void RemoveTokenFromPlatform(Address from, string platform, string symbol)
        {
            Runtime.Expect(from.IsUser, "witness failed");
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");
            Runtime.Expect(Nexus.IsDangerousAddress(from), "invalid target address");
            Runtime.Expect(Runtime.TokenExists(symbol), "token not registered");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(tokenInfo.IsTransferable(), "token not transferable");
            Runtime.Expect(tokenInfo.IsFungible(), "token not fungible");

            var platformsForAddress = GetPlatformsForAddress(from);
            var platformInfoIndex = platformsForAddress.ToList().FindIndex(p => p.Name == platform);
            var platformInfo = platformsForAddress.FirstOrDefault(p => p.Name == platform);
            Runtime.Expect(platformInfo.Name == platform, "platform not registered");

            var tokens = platformInfo.Tokens.ToList();
            var token = tokens.FirstOrDefault(t => t.Symbol == symbol);
            Runtime.Expect(token.Symbol == symbol, "token not registered");

            tokens.Remove(token);
            platformInfo.Tokens = tokens.ToArray();

            platformsForAddress[platformInfoIndex] = platformInfo;
            _platformsAddresses.Set(from, platformsForAddress);

            UpdateSwapper(platform, symbol, token.ExternalContractAddress, platformInfo.LocalAddress,
                platformInfo.ExternalAddress, false);
        }

        /// <summary>
        /// Get all platforms registered for a specific address
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public PlatformDetails[] GetPlatformsForAddress(Address from)
        {
            return _platformsAddresses.Get<Address, PlatformDetails[]>(from);
        }

        /// <summary>
        /// Get platform info for a specific address
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public PlatformDetails GetPlatformDetailsForAddress(Address from, string platform)
        {
            var platformInfos = _platformsAddresses.Get<Address, PlatformDetails[]>(from);
            if (platformInfos == null)
            {
                return new PlatformDetails();
            }

            foreach (var platformInfo in platformInfos)
            {
                if (platformInfo.Name == platform)
                {
                    return platformInfo;
                }
            }

            return new PlatformDetails();
        }

        /// <summary>
        /// Check if a specific address has platform info
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public bool HasPlatformInfo(Address from, string platform)
        {
            var platformInfos = _platformsAddresses.Get<Address, PlatformDetails[]>(from);
            if (platformInfos == null)
            {
                return false;
            }

            foreach (var platformInfo in platformInfos)
            {
                if (platformInfo.Name == platform)
                {
                    return true;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if it's a valid platform.
        /// </summary>
        /// <param name="platform"></param>
        /// <returns></returns>
        private bool IsValidPlatform(string platform)
        {
            return platform.Equals("ethereum", StringComparison.OrdinalIgnoreCase) ||
                   platform.Equals("bsc", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get all Platforms (Swappers)
        /// </summary>
        /// <returns></returns>
        public PlatformDetails[] GetAllPlatforms()
        {
            return _platformsAddresses
                .AllValues<PlatformDetails[]>()
                .SelectMany(p => p)
                .ToArray();
        }

        /// <summary>
        /// Get Available Swappers for a specific platform and symbol
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public PlatformDetails[] GetAvailableSwappers(string platform, string symbol)
        {
            return _platformsAddresses
                .AllValues<PlatformDetails[]>()
                .SelectMany(p => p)
                .Where(p => p.Tokens.Any(t => t.Symbol == symbol) && p.Name == platform)
                .ToArray();
        }

        /// <summary>
        /// Get Available Main Swappers (For Wallets) for a specific platform and symbol
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public PlatformDetails[] GetAvailableMainSwappers(string platform, string symbol)
        {
            return _platformsAddresses
                .AllValues<PlatformDetails[]>()
                .SelectMany(p => p)
                .Where(p => p.MainSwapper && p.Name == platform && 
                            p.Tokens.Any(t => t.Symbol == symbol))
                .ToArray();
        }

        /// <summary>
        /// Sends tokens to a specific platform (From Phantasma to another platform)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="externalAddress"></param>
        /// <param name="fromPlatform"></param>
        /// <param name="toPlatform"></param>
        /// <param name="symbol"></param>
        /// <param name="amount"></param>
        public void SendTokensToPlatform(Address from, string externalAddress, string fromPlatform, string toPlatform,
            string symbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(fromPlatform != toPlatform, "platforms must be different");
            Runtime.Expect(fromPlatform == DomainSettings.PlatformName, "platform must be Phantasma");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");
            var tokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(tokenInfo.IsTransferable(), "token not transferable");
            Runtime.Expect(tokenInfo.IsFungible(), "token not fungible");

            Runtime.Expect(amount > 0, "invalid amount");
            Runtime.Expect(amount < UnitConversion.ToBigInteger(_maximumTransferAmount, tokenInfo.Decimals),
                "invalid amount");

            // Get all Platforms
            var availableSwappers = GetAvailableSwappers(toPlatform, symbol);
            Runtime.Expect(availableSwappers.Length > 0, "no available swappers");

            var chainTransfer = new CrossChainTransfer()
            {
                FromExternalChain = false,
                Identifier = from.Text + externalAddress + fromPlatform + toPlatform + symbol + amount + Runtime.Time,
                status = CrossChainTransferStatus.Pending,
                FromUserAddress = from,
                UserExternalAddress = externalAddress,
                Swapper = Address.Null,
                Symbol = symbol,
                Amount = amount,
                PhantasmaHash = Runtime.Transaction.Hash,
                FromPlatform = fromPlatform,
                ToPlatform = toPlatform,
                StartedAt = Runtime.Time,
                UpdatedAt = Runtime.Time,
            };

            Runtime.TransferTokens(symbol, from, this.Address, amount);
            var storageList = _crossChainTransfers.Get<Address, StorageList>(from);
            storageList.Add<CrossChainTransfer>(chainTransfer);
            _crossChainTransfers.Set<Address, StorageList>(from, storageList);
        }

        /// <summary>
        /// Get all Cross Chain Transfers.
        /// </summary>
        /// <returns></returns>
        public CrossChainTransfer[] GetAllCrossChainTransfers()
        {
            return _crossChainTransfers
                .AllValues<StorageList>()
                .Select(s => s.All<CrossChainTransfer>())
                .SelectMany(c => c)
                .ToArray();
        }

        /// <summary>
        /// Get Cross Chain Tranfers that are pending or in progress.
        /// </summary>
        /// <returns></returns>
        public CrossChainTransfer[] GetCrossChainTransfers()
        {
            return _crossChainTransfers
                .AllValues<StorageList>()
                .Select(s => s.All<CrossChainTransfer>())
                .SelectMany(c => c)
                .Where(c => c.status == CrossChainTransferStatus.Pending ||
                            c.status == CrossChainTransferStatus.InProgress &&
                            !c.FromExternalChain)
                .ToArray();
        }

        /// <summary>
        /// Get all the transfers of a user.
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public CrossChainTransfer[] GetCrossChainTransfersForUser(Address from)
        {
            return _crossChainTransfers
                .AllValues<StorageList>()
                .Select(s => s.All<CrossChainTransfer>())
                .SelectMany(c => c)
                .Where(c => c.FromUserAddress == from)
                .ToArray();
        }

        /// <summary>
        /// Get all the transfers of that swapper.
        /// </summary>
        /// <param name="swapper"></param>
        /// <returns></returns>
        public CrossChainTransfer[] GetCrossChainTransfersForSwapper(Address swapper)
        {
            return _crossChainTransfers
                .AllValues<StorageList>()
                .Select(s => s.All<CrossChainTransfer>())
                .SelectMany(c => c)
                .Where(c => c.status == CrossChainTransferStatus.Pending ||
                            c.status == CrossChainTransferStatus.InProgress &&
                            c.Swapper == swapper)
                .ToArray();
        }

        /// <summary>
        /// Get all the external transfer for that swapper.
        /// </summary>
        /// <param name="swapper"></param>
        /// <returns></returns>
        public CrossChainTransfer[] GetExternalCrossChainTransfersForSwapper(Address swapper)
        {
            return _crossChainTransfers
                .AllValues<StorageList>()
                .Select(s => s.All<CrossChainTransfer>())
                .SelectMany(c => c)
                .Where(c => c.status == CrossChainTransferStatus.Pending ||
                            c.status == CrossChainTransferStatus.InProgress &&
                            c.Swapper == swapper && c.FromExternalChain)
                .ToArray();
        }

        /// <summary>
        /// Check if a transfer exists.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        public bool HasCrossChainTransfer(Address from, Hash hash)
        {
            return _crossChainTransfers
                .ContainsKey(from) && _crossChainTransfers
                .Get<Address, StorageList>(from)
                .All<CrossChainTransfer>()
                .ToList()
                .Any(c => c.PhantasmaHash == hash);
        }

        /// <summary>
        /// Claim Cross Chain Transfer to process.
        /// This method is called by the Swapper to claim a transfer to process.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <param name="identifier"></param>
        public void AcceptCrossChainTransfer(Address from, string platform, string identifier)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");

            var platformDetail = GetPlatformDetailsForAddress(from, platform);
            Runtime.Expect(platformDetail.Name == platform, "platform not registered");

            var crossChainTransfers = GetCrossChainTransfers();
            var crossChainTransfer = crossChainTransfers.FirstOrDefault(c => c.Identifier == identifier);
            Runtime.Expect(crossChainTransfer.Identifier == identifier, "invalid identifier");
            if (crossChainTransfer.status == CrossChainTransferStatus.InProgress)
            {
                // 1 Hour to complete
                Runtime.Expect(crossChainTransfer.UpdatedAt.Value + 3600 > Runtime.Time,
                    "CrossChainTransfer still processing");
            }

            // Update details
            var fee = crossChainTransfer.Amount * platformDetail.FeePercentage / 100;
            crossChainTransfer.status = CrossChainTransferStatus.InProgress;
            crossChainTransfer.Swapper = from;
            crossChainTransfer.SwapperExternalAddress = platformDetail.ExternalAddress;
            crossChainTransfer.UpdatedAt = Runtime.Time;
            crossChainTransfer.Fee = fee;
            crossChainTransfer.AmountSubFee = crossChainTransfer.Amount - fee;

            // Store it
            var storageList = _crossChainTransfers.Get<Address, StorageList>(crossChainTransfer.FromUserAddress);
            var allCross = storageList.All<CrossChainTransfer>();
            var crossChainTransferIndex = allCross.ToList().FindIndex(c => c.Identifier == identifier);
            storageList.Replace(crossChainTransferIndex, crossChainTransfer);
            _crossChainTransfers.Set<Address, StorageList>(crossChainTransfer.FromUserAddress, storageList);
        }

        /// <summary>
        /// Complete Cross Chain Transfer
        /// This is for the blockchain to validate the transfer on the other chain.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <param name="identifier"></param>
        /// <param name="hash"></param>
        public void CompleteCrossChainTransfer(Address from, string platform, string identifier, Hash hash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");
            Runtime.Expect(!hash.IsNull, "invalid hash");

            var platformDetail = GetPlatformDetailsForAddress(from, platform);
            Runtime.Expect(platformDetail.Name == platform, "platform not registered");
            var crossChainTransfers = GetCrossChainTransfers();
            var crossChainTransfer = crossChainTransfers.FirstOrDefault(c => c.Identifier == identifier);
            Runtime.Expect(crossChainTransfer.Identifier == identifier, "invalid identifier");
            Runtime.Expect(!crossChainTransfer.FromExternalChain,
                "Call the CompleteInternalCrossChainTransfer method instead.");

            bool isValid = false;
            var resultTransactionData = Runtime.ReadCrossChainTransactionFromOracle(platform, "main", hash);
            // Ethereum == 2 // BSC == 3
            byte platformId = platform.Equals("ethereum", StringComparison.InvariantCulture) ? (byte)3
                : platform.Equals("bsc", StringComparison.InvariantCulture) ? (byte)2 : (byte)2;
            var userExternalAddress = Address.EncodeAddress(platformId, crossChainTransfer.UserExternalAddress);
            var swapperExternalAddress = Address.EncodeAddress(platformId, crossChainTransfer.SwapperExternalAddress);

            if (resultTransactionData != null)
            {
                var transfers = resultTransactionData.Transfers.ToList();
                foreach (var transfer in transfers)
                {
                    if (transfer.sourceAddress == swapperExternalAddress &&
                        transfer.destinationAddress == userExternalAddress &&
                        transfer.Symbol == crossChainTransfer.Symbol &&
                        transfer.Value == crossChainTransfer.Amount)
                    {
                        isValid = true;
                        break;
                    }
                }
            }

            // Validate the transaction
            Runtime.Expect(isValid, "invalid transfer");
            crossChainTransfer.status = CrossChainTransferStatus.Confirmed;
            crossChainTransfer.ExternalHash = hash;
            crossChainTransfer.UpdatedAt = Runtime.Time;
            crossChainTransfer.EndedAt = Runtime.Time;

            // Store it 
            var storageList = _crossChainTransfers.Get<Address, StorageList>(crossChainTransfer.FromUserAddress);
            var allCrossChainTransfers = storageList.All<CrossChainTransfer>().ToList();
            var index = allCrossChainTransfers.FindIndex(c => c.Identifier == identifier);
            storageList.Replace(index, crossChainTransfer);
            _crossChainTransfers.Set<Address, StorageList>(crossChainTransfer.FromUserAddress, storageList);

            // Pay the Swapper for the service
            Runtime.TransferTokens(crossChainTransfer.Symbol, this.Address, crossChainTransfer.Swapper,
                crossChainTransfer.Amount);
            Runtime.Notify(EventKind.ChainSwap, from,
                new TransactionSettleEventData(hash, platform, crossChainTransfer.ToPlatform));
        }

        /// <summary>
        /// Complete Internal Cross Chain this is gonna be called by the swapper.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <param name="identifier"></param>
        public void CompleteInternalCrossChainTransfer(Address from, string platform, string identifier)
        {
            Runtime.Expect(from.IsUser, "invalid user");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");

            var platformDetail = GetPlatformDetailsForAddress(from, platform);
            Runtime.Expect(platformDetail.Name == platform, "platform not registered");
            var crossChainTransfers = GetCrossChainTransfers();
            var crossChainTransfer = crossChainTransfers.FirstOrDefault(c => c.Identifier == identifier);
            Runtime.Expect(crossChainTransfer.Identifier == identifier, "invalid identifier");
            Runtime.Expect(crossChainTransfer.FromExternalChain, "Call the CompleteCrossChainTransfer method instead.");

            var swapperBalance = Runtime.GetBalance(crossChainTransfer.Symbol, from);
            if (swapperBalance < crossChainTransfer.AmountSubFee)
            {
                var symbol = crossChainTransfer.Symbol == DomainSettings.StakingTokenSymbol
                    ? DomainSettings.FuelTokenSymbol
                    : DomainSettings.StakingTokenSymbol;

                var swapperOtherSymbolBalance = Runtime.GetBalance(symbol, from);
                var tokenDetails = Runtime.GetToken(symbol);

                // Get Rate from Exchange
                var rate = Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate),
                    crossChainTransfer.Symbol,
                    symbol,
                    crossChainTransfer.AmountSubFee - swapperBalance
                ).AsNumber();

                if (swapperOtherSymbolBalance < rate)
                {
                    symbol = symbol == DomainSettings.StakingTokenSymbol
                        ? DomainSettings.FuelTokenSymbol
                        : DomainSettings.StakingTokenSymbol;
                    swapperOtherSymbolBalance = Runtime.GetBalance(symbol, this.Address);
                    rate = Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.GetRate),
                        crossChainTransfer.Symbol,
                        symbol,
                        crossChainTransfer.AmountSubFee - swapperBalance
                    ).AsNumber();

                    Runtime.Expect(swapperOtherSymbolBalance >= rate, "insufficient balance");
                }

                // Swapp Tokens
                Runtime.CallNativeContext(NativeContractKind.Exchange, nameof(ExchangeContract.SwapTokens),
                    from,
                    symbol,
                    crossChainTransfer.Symbol,
                    rate
                );
            }

            // Transfer the tokens
            Runtime.TransferTokens(crossChainTransfer.Symbol, from, crossChainTransfer.FromUserAddress,
                crossChainTransfer.AmountSubFee);

            // Update the status
            crossChainTransfer.status = CrossChainTransferStatus.Confirmed;
            crossChainTransfer.PhantasmaHash = Runtime.Transaction.Hash;
            crossChainTransfer.UpdatedAt = Runtime.Time;
            crossChainTransfer.EndedAt = Runtime.Time;

            // Store it 
            var storageList = _crossChainTransfers.Get<Address, StorageList>(crossChainTransfer.FromUserAddress);
            var allCrossChainTransfers = storageList.All<CrossChainTransfer>().ToList();
            var index = allCrossChainTransfers.FindIndex(c => c.Identifier == identifier);
            storageList.Replace(index, crossChainTransfer);
            _crossChainTransfers.Set<Address, StorageList>(crossChainTransfer.FromUserAddress, storageList);

            Runtime.Notify(EventKind.ChainSwap, from,
                new TransactionSettleEventData(crossChainTransfer.ExternalHash, platform, "main"));
        }

        /// <summary>
        /// This is used to settle a Cross Chain Transaction from the other chain to Phantasma.
        /// </summary>
        /// <param name="Caller">Validator</param>
        /// <param name="from">USER</param>
        /// <param name="platform">ETH / BSC</param>
        /// <param name="chain">ethereum</param>
        /// <param name="hash"></param>
        public void SettleCrossChainTransaction(Address Caller, Address from, string externalAddress, string platform,
            string chain, Hash hash)
        {
            // From USER
            // Platform = ETH / BSC
            // Chain = ETH / BSC
            // Hash = Hash of the transaction on the other chain
            Runtime.Expect(Runtime.IsWitness(Caller), "invalid witness");
            Runtime.Expect(Runtime.IsKnownValidator(Caller), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(platform == DomainSettings.PlatformName,
                "invalid platform, this method can only be called from other chains.");
            Runtime.Expect(!hash.IsNull, "Invalid Hash");

            // Check if this transaction already exists
            Runtime.Expect(!HasCrossChainTransfer(from, hash), "Cross Chain Transaction already exists");

            var platformDetails = GetAllPlatforms();

            // From the hash get the transaction
            var resultTransactionData = Runtime.ReadCrossChainTransactionFromOracle(platform, "main", hash);
            Runtime.Expect(resultTransactionData.Hash == hash, "unxpected hash");

            // Get the address the received the transaction
            byte platformId = platform.Equals("ethereum", StringComparison.InvariantCulture) ? (byte)3
                : platform.Equals("bsc", StringComparison.InvariantCulture) ? (byte)2 : (byte)2;
            var userExternalAddress = Address.EncodeAddress(platformId, externalAddress);
            
            
            Runtime.Expect(userExternalAddress == resultTransactionData.Transfers[0].sourceAddress,
                "invalid external address");
            var transfer = resultTransactionData.Transfers[0];

            // Check if the address is registered 
            var platformInfo = platformDetails.FirstOrDefault(p =>
                Address.EncodeAddress(platformId, p.ExternalAddress) ==
                resultTransactionData.Transfers[0].destinationAddress);
            var existsPlatform = platformDetails.Any(p =>
                Address.EncodeAddress(platformId, p.ExternalAddress) ==
                resultTransactionData.Transfers[0].destinationAddress);
            Runtime.Expect(existsPlatform, "Invalid Swapper Address");

            Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
            var token = Runtime.GetToken(transfer.Symbol);

            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");

            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

            Runtime.Expect(transfer.interopAddress.IsUser, "invalid destination address");

            var fee = transfer.Value * platformInfo.FeePercentage / 100;
            var crossChainTransfer = new CrossChainTransfer()
            {
                FromExternalChain = true,
                Identifier = from.Text + externalAddress + platform + "phantasma" + transfer.Symbol + hash +
                             Runtime.Time,
                status = CrossChainTransferStatus.Pending,
                FromUserAddress = from,
                UserExternalAddress = externalAddress,
                Swapper = platformInfo.LocalAddress,
                SwapperExternalAddress = platformInfo.ExternalAddress,
                Symbol = transfer.Symbol,
                Amount = transfer.Value,
                Fee = fee,
                PhantasmaHash = Hash.Null,
                ExternalHash = hash,
                FromPlatform = platform,
                ToPlatform = "phantasma",
                StartedAt = Runtime.Time,
                UpdatedAt = Runtime.Time,
            };

            // Store it 
            var storageList = _crossChainTransfers.Get<Address, StorageList>(from);
            storageList.Add<CrossChainTransfer>(crossChainTransfer);
            _crossChainTransfers.Set<Address, StorageList>(from, storageList);
        }
        
        #region Betting

        /// <summary>
        /// Get All Cross Chain Bets
        /// </summary>
        /// <returns></returns>
        public CrossChainTransfer[] GetAllCrossChainBets()
        {
            return _crossChainBetting.AllKeys<CrossChainTransfer>();
        }
        
        /// <summary>
        /// Get Cross Chain Bet by Identifier
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public CrossChainTransfer GetCrossChainBet(string identifier)
        {
            return _crossChainBetting
                .AllKeys<CrossChainTransfer>()
                .Where(c => c.Identifier == identifier)
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Get Cross Chain all the Bets for a transaction.
        /// </summary>
        /// <param name="crossChainSwap"></param>
        /// <returns></returns>
        public Bet[] GetBetsForTransaction(CrossChainTransfer crossChainSwap)
        {
            return _crossChainBetting
                .Get<CrossChainTransfer, StorageList>(crossChainSwap)
                .All<Bet>();
        }
        
        /// <summary>
        /// Add a Bet to a Cross Chain Transaction
        /// </summary>
        /// <param name="crossChainSwap"></param>
        /// <param name="bet"></param>
        private void AddBet(CrossChainTransfer crossChainSwap, Bet bet)
        {
            var storageList = _crossChainBetting.Get<CrossChainTransfer, StorageList>(crossChainSwap);
            storageList.Add<Bet>(bet);
            _crossChainBetting.Set<CrossChainTransfer, StorageList>(crossChainSwap, storageList);
        }
        
        /// <summary>
        /// Update a Bet to a Cross Chain Transaction
        /// </summary>
        /// <param name="crossChainSwap"></param>
        /// <param name="bet"></param>
        private void UpdateBet(CrossChainTransfer crossChainSwap, Bet bet)
        {
            var storageList = _crossChainBetting.Get<CrossChainTransfer, StorageList>(crossChainSwap);
            var bets = storageList.All<Bet>().ToList();
            var index = bets.FindIndex(b => b.Swapper == bet.Swapper);
            storageList.Replace(index, bet);
            _crossChainBetting.Set<CrossChainTransfer, StorageList>(crossChainSwap, storageList);
        }
        
        /// <summary>
        /// Returns true if the Cross Chain Transaction has a Bet from the Swapper
        /// </summary>
        /// <param name="crossChainSwap"></param>
        /// <param name="swapper"></param>
        /// <returns></returns>
        private bool HasBet(CrossChainTransfer crossChainSwap, Address swapper)
        {
            return _crossChainBetting
                .Get<CrossChainTransfer, StorageList>(crossChainSwap)
                .All<Bet>()
                .Any<Bet>(b => b.Swapper == swapper);
        }
        
        /// <summary>
        /// Request a Cross Chain Swap
        /// </summary>
        /// <param name="caller"></param>
        /// <param name="from"></param>
        /// <param name="externalAddress"></param>
        /// <param name="platform"></param>
        /// <param name="chain"></param>
        /// <param name="symbol"></param>
        /// <param name="amount"></param>
        public void RequestCrossChainSwap(Address caller, Address from, string externalAddress, string platform,
            string chain, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "invalid amount");
            Runtime.Expect(Runtime.IsWitness(caller), "invalid witness");
            Runtime.Expect(Runtime.IsKnownValidator(caller), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(platform != DomainSettings.PlatformName, "invalid platform");
            Runtime.Expect(chain != DomainSettings.RootChainName, "invalid chain");
            
            var crossChainTransfer = new CrossChainTransfer()
            {
                FromExternalChain = true,
                Identifier = from.Text + externalAddress + platform + Runtime.Transaction.Hash.ToString() + "phantasma" + symbol + amount + chain + Runtime.Time,
                status = CrossChainTransferStatus.Pending,
                FromUserAddress = from,
                UserExternalAddress = externalAddress,
                Swapper = Address.Null,
                SwapperExternalAddress = "",
                Symbol = symbol,
                Amount = amount,
                Fee = 0,
                PhantasmaHash = Runtime.Transaction.Hash,
                ExternalHash = Hash.Null,
                FromPlatform = platform,
                ToPlatform = "phantasma",
                StartedAt = Runtime.Time,
                UpdatedAt = Runtime.Time,
            };

            // Store it 
            var storageList = _crossChainTransfers.Get<Address, StorageList>(from);
            storageList.Add<CrossChainTransfer>(crossChainTransfer);
            _crossChainTransfers.Set<Address, StorageList>(from, storageList);
        }

        /// <summary>
        /// This method is called by Swappers that want to bet on a cross chain swap
        /// </summary>
        public void BetOnCrossChainSwap(Address from, string identifier, string platform, BigInteger BetFee)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(BetFee >= 0, "invalid fee");
            Runtime.Expect(BetFee >= _minimumFeePercentage, "invalid fee");
            Runtime.Expect(BetFee <= _maximumFeePercentage, "invalid fee");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");

            var platformDetail = GetPlatformDetailsForAddress(from, platform);
            Runtime.Expect(platformDetail.Name == platform, "platform not registered");
            
            var crossChainSwap = GetCrossChainBet(identifier);
            Runtime.Expect(crossChainSwap.Identifier != null, "invalid cross chain swap");
            Runtime.Expect(!IsBettingPeriodEnded(crossChainSwap), "Betting period ended");
            
            var lowestFee = GetLowestFeeForBet(from, identifier, platform);
            Runtime.Expect(BetFee >= lowestFee, "The lowest fee is " + lowestFee + "%");
            
            // Check if the bet is valid
            // Save the Bet in the cross chain swap
            var bet = new Bet()
            {
                Swapper = from,
                platform = platform,
                externalAddress = platformDetail.ExternalAddress,
                BetFeeAmount = BetFee,
                symbol = crossChainSwap.Symbol,
                CreatedAt = Runtime.Time,
                UpdatedAt = Runtime.Time,
            };
            
            if ( HasBet(crossChainSwap, from) )
            {
                UpdateBet(crossChainSwap, bet);
            }
            else
            {
                Runtime.TransferTokens(crossChainSwap.Symbol, from, this.Address, crossChainSwap.Amount);

                AddBet(crossChainSwap, bet);
            }
            
            if (IsBettingPeriodEnded(crossChainSwap))
            {
                // Transfer all the bets to the Swappers
                var bets = GetBetsForTransaction(crossChainSwap);
                foreach (var b in bets)
                {
                    if (from == b.Swapper)
                        continue;
                    
                    Runtime.TransferTokens(crossChainSwap.Symbol, this.Address, b.Swapper, crossChainSwap.Amount);
                }
                
                // Update the cross chain swap status
                crossChainSwap.status = CrossChainTransferStatus.InProgress;
                crossChainSwap.UpdatedAt = Runtime.Time;
                crossChainSwap.Swapper = from;
                crossChainSwap.SwapperExternalAddress = platformDetail.ExternalAddress;
                crossChainSwap.Fee = BetFee;
                crossChainSwap.AmountSubFee = crossChainSwap.Amount - (crossChainSwap.Amount * crossChainSwap.Fee / 100);
                
                // Update to the cross chain swap the total amount of bets
                var storageList = _crossChainTransfers.Get<Address, StorageList>(from);
                var index = storageList.All<CrossChainTransfer>().ToList().FindIndex(c => c.Identifier == crossChainSwap.Identifier);
                storageList.Replace(index, crossChainSwap);
                _crossChainTransfers.Set<Address, StorageList>(from, storageList);
                _crossChainBetWinners.Set<CrossChainTransfer, Address>(crossChainSwap, from);
            }
        }
        
        /// <summary>
        /// Returns the if the best was ended or not
        /// </summary>
        /// <param name="crossChainSwap"></param>
        /// <returns></returns>
        private bool IsBettingPeriodEnded(CrossChainTransfer crossChainSwap)
        {
            //var _betDur = Runtime.GetGovernanceValue("CrossChainSwapDuration");
            //Timestamp _betDurTimestamp = uint.Parse(_betDur.ToString());
            return Runtime.Time > crossChainSwap.StartedAt.Value + _bettingDuration;
        }
        
        /// <summary>
        /// Returns the Lowest fee for a bet
        /// </summary>
        /// <param name="from"></param>
        /// <param name="identifier"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public BigInteger GetLowestFeeForBet(Address from, string identifier, string platform)
        {
            var crossChainBet = GetCrossChainBet(identifier);
            var bets = GetBetsForTransaction(crossChainBet);
            return bets.OrderBy(bet => bet.BetFeeAmount).FirstOrDefault().BetFeeAmount;
        }

        /// <summary>
        /// Returns the cross chain swap bet winner.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public string GetCrossChainSwapAddressToSwap(Address from, string platform)
        {
            return "";
        }
        #endregion

        #endregion

        #region Old Methods
        [ObsoleteAttribute("This method is obsolete.", true)]
        public void SettleTransaction(Address from, string platform, string chain, Hash hash)
        {
            Runtime.Expect(Runtime.ProtocolVersion < 13, "this method is obsolete");
            Runtime.Expect(false, "this method is obsolete");


            Runtime.Expect(!Filter.Enabled, "swap settlements disabled");

            PlatformSwapAddress[] swapAddresses;

            if (platform != DomainSettings.PlatformName)
            {
                Runtime.Expect(Runtime.PlatformExists(platform), "unsupported platform");
                var platformInfo = Runtime.GetPlatformByName(platform);
                swapAddresses = platformInfo.InteropAddresses;
            }
            else
            {
                swapAddresses = null;
            }

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");

            var chainHashes = _platformHashes.Get<string, StorageMap>(platform);
            Runtime.Expect(!chainHashes.ContainsKey(hash), "hash already seen");

            var interopTx = Runtime.ReadTransactionFromOracle(platform, chain, hash);

            Runtime.Expect(interopTx.Hash == hash, "unxpected hash");

            int swapCount = 0;

            foreach (var transfer in interopTx.Transfers)
            {
                var count = _withdraws.Count();
                var index = -1;
                for (int i = 0; i < count; i++)
                {
                    var entry = _withdraws.Get<InteropWithdraw>(i);
                    if (entry.destination == transfer.destinationAddress && entry.transferAmount == transfer.Value &&
                        entry.transferSymbol == transfer.Symbol)
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                    var token = Runtime.GetToken(transfer.Symbol);

                    if (token.Flags.HasFlag(TokenFlags.Fungible))
                    {
                        Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");
                    }
                    else
                    {
                        Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value),
                            $"nft {transfer.Value} must exist");
                    }

                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

                    var withdraw = _withdraws.Get<InteropWithdraw>(index);
                    _withdraws.RemoveAt(index);

                    var org = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
                    Runtime.Expect(org.IsMember(from), $"{from.Text} is not a validator node");
                    Runtime.TransferTokens(withdraw.feeSymbol, Address, from, withdraw.feeAmount);

                    RegisterHistory(hash, withdraw.hash, DomainSettings.PlatformName, Runtime.Chain.Name,
                        transfer.sourceAddress, hash, platform, chain, withdraw.destination, transfer.Symbol,
                        transfer.Value);
                    swapCount++;
                }
                else if (swapAddresses != null)
                {
                    foreach (var entry in swapAddresses)
                    {
                        if (transfer.destinationAddress == entry.LocalAddress)
                        {
                            Runtime.Expect(!transfer.sourceAddress.IsNull, "invalid source address");

                            // Here we detect if this transfer occurs between two swap addresses
                            var isInternalTransfer = Runtime.IsPlatformAddress(transfer.sourceAddress);

                            if (!isInternalTransfer)
                            {
                                Runtime.Expect(Runtime.TokenExists(transfer.Symbol), "invalid token");
                                var token = Runtime.GetToken(transfer.Symbol);

                                if (token.Flags.HasFlag(TokenFlags.Fungible))
                                {
                                    Runtime.Expect(transfer.Value > 0, "amount must be positive and greater than zero");
                                }
                                else
                                {
                                    Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value),
                                        $"nft {transfer.Value} must exist");
                                }


                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable),
                                    "token must be transferable");
                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable),
                                    "transfer token must be swappable");

                                Runtime.Expect(transfer.interopAddress.IsUser, "invalid destination address");

                                Runtime.SwapTokens(platform, transfer.sourceAddress, Runtime.Chain.Name,
                                    transfer.interopAddress, transfer.Symbol, transfer.Value);

                                if (!token.Flags.HasFlag(TokenFlags.Fungible))
                                {
                                    var externalNft =
                                        Runtime.ReadNFTFromOracle(platform, transfer.Symbol, transfer.Value);
                                    var ram = externalNft.Serialize();

                                    var localNft = Runtime.ReadToken(transfer.Symbol, transfer.Value);
                                    Runtime.WriteToken(from, transfer.Symbol, transfer.Value,
                                        ram); // TODO "from" here might fail due to contract triggers, review this later
                                }

                                var settleHash = Runtime.Transaction.Hash;
                                RegisterHistory(settleHash, hash, platform, chain, transfer.sourceAddress, settleHash,
                                    DomainSettings.PlatformName, Runtime.Chain.Name, transfer.interopAddress,
                                    transfer.Symbol, transfer.Value);

                                swapCount++;
                            }

                            break;
                        }
                    }
                }
            }

            Runtime.Expect(swapCount > 0, "nothing to settle");
            chainHashes.Set(hash, Runtime.Transaction.Hash);
            Runtime.Notify(EventKind.ChainSwap, from, new TransactionSettleEventData(hash, platform, chain));
        }

        /// <summary>
        /// Send to external chain
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="symbol"></param>
        /// <param name="value"></param>
        [ObsoleteAttribute("This method is obsolete.", true)]
        public void WithdrawTokens(Address from, Address to, string symbol, BigInteger value)
        {
            Runtime.Expect(Runtime.ProtocolVersion < 13, "this method is obsolete");
            Runtime.Expect(false, "this method is obsolete");

            Runtime.Expect(!Filter.Enabled, "swap withdraws disabled");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from.IsUser, "source must be user address");
            Runtime.Expect(to.IsInterop, "destination must be interop address");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");

            var transferTokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Transferable),
                "transfer token must be transferable");
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

            if (transferTokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                Runtime.Expect(value > 0, "amount must be positive and greater than zero");
            }
            else
            {
                Runtime.Expect(Runtime.NFTExists(symbol, value), $"nft {value} must be exist");
            }

            byte platformID;
            byte[] dummy;
            to.DecodeInterop(out platformID, out dummy);
            Runtime.Expect(platformID > 0, "invalid platform ID");
            var platform = Runtime.GetPlatformByIndex(platformID);
            Runtime.Expect(platform != null, "invalid platform");

            int interopIndex = -1;
            for (int i = 0; i < platform.InteropAddresses.Length; i++)
            {
                if (platform.InteropAddresses[i].LocalAddress == to)
                {
                    interopIndex = i;
                    break;
                }
            }

            var platformTokenHash = Runtime.GetTokenPlatformHash(symbol, platform);
            Runtime.Expect(platformTokenHash != Hash.Null, $"invalid foreign token hash {platformTokenHash}");

            Runtime.Expect(interopIndex == -1, "invalid target address");

            var feeSymbol = platform.Symbol;
            Runtime.Expect(Runtime.TokenExists(feeSymbol), "invalid fee token");

            var feeTokenInfo = Runtime.GetToken(feeSymbol);
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Fungible), "fee token must be fungible");
            Runtime.Expect(feeTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "fee token must be transferable");

            BigInteger feeAmount;
            feeAmount = Runtime.ReadFeeFromOracle(platform.Name); // fee is in fee currency (gwei for eth, gas for neo)

            Runtime.Expect(feeAmount > 0, "fee is too small");

            var feeBalance = Runtime.GetBalance(feeSymbol, from);
            if (feeBalance < feeAmount)
            {
                Runtime.CallNativeContext(NativeContractKind.Swap, nameof(SwapContract.SwapReverse), from,
                    DomainSettings.FuelTokenSymbol, feeSymbol, feeAmount);

                feeBalance = Runtime.GetBalance(feeSymbol, from);
                Runtime.Expect(feeBalance >= feeAmount, $"missing {feeSymbol} for interop swap");
            }

            Runtime.TransferTokens(feeSymbol, from, Address, feeAmount);

            Runtime.SwapTokens(Runtime.Chain.Name, from, platform.Name, to, symbol, value);

            var withdraw = new InteropWithdraw()
            {
                destination = to,
                transferAmount = value,
                transferSymbol = symbol,
                feeAmount = feeAmount,
                feeSymbol = feeSymbol,
                hash = Runtime.Transaction.Hash,
                timestamp = Runtime.Time
            };
            _withdraws.Add(withdraw);
        }

        [ObsoleteAttribute("This method is obsolete.", true)]
        public Hash GetSettlement(string platformName, Hash hash)
        {
            var chainHashes = _platformHashes.Get<string, StorageMap>(platformName);
            if (chainHashes.ContainsKey(hash))
            {
                return chainHashes.Get<Hash, Hash>(hash);
            }

            return Hash.Null;
        }
        
        [ObsoleteAttribute("This method is obsolete.", true)]
        public InteropTransferStatus GetStatus(string platformName, Hash hash)
        {
            var chainHashes = _platformHashes.Get<string, StorageMap>(platformName);
            if (chainHashes.ContainsKey(hash))
            {
                return InteropTransferStatus.Confirmed;
            }

            var count = _withdraws.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = _withdraws.Get<InteropWithdraw>(i);
                if (entry.hash == hash)
                {
                    return InteropTransferStatus.Pending;
                }
            }


            return InteropTransferStatus.Unknown;
        }

        #region SWAP HISTORY
        [ObsoleteAttribute("This method is obsolete.", true)]
        private void RegisterHistory(Hash swapHash, Hash sourceHash, string sourcePlatform, string sourceChain,
            Address sourceAddress, Hash destHash, string destPlatform, string destChain, Address destAddress,
            string symbol, BigInteger value)
        {
            var entry = new InteropHistory()
            {
                sourceAddress = sourceAddress,
                sourceHash = sourceHash,
                sourcePlatform = sourcePlatform,
                sourceChain = sourceChain,
                destAddress = destAddress,
                destHash = destHash,
                destPlatform = destPlatform,
                destChain = destChain,
                symbol = symbol,
                value = value,
            };

            _swapMap.Set(swapHash, entry);

            AppendToHistoryMap(swapHash, sourceAddress);
            AppendToHistoryMap(swapHash, destAddress);
        }

        [ObsoleteAttribute("This method is obsolete.", true)]
        private void AppendToHistoryMap(Hash swapHash, Address target)
        {
            var list = _historyMap.Get<Address, StorageList>(target);
            list.Add(swapHash);
        }

        [ObsoleteAttribute("This method is obsolete.", true)]
        public InteropHistory[] GetSwapsForAddress(Address address)
        {
            var list = _historyMap.Get<Address, StorageList>(address);
            var count = (int)list.Count();

            var result = new InteropHistory[count];
            for (int i = 0; i < count; i++)
            {
                var hash = list.Get<Hash>(i);
                result[i] = _swapMap.Get<Hash, InteropHistory>(hash);
            }

            return result;
        }

        #endregion

        #endregion
    }
}
