using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public enum InteropTransferStatus
    {
        Unknown,
        Pending,
        Confirmed
    }
    
    public enum CrossChainTransferStatus
    {
        Pending,
        InProgress,
        Confirmed
    }

    public struct InteropWithdraw
    {
        public Hash hash;
        public Address destination;
        public string transferSymbol;
        public BigInteger transferAmount;
        public string feeSymbol;
        public BigInteger feeAmount;
        public Timestamp timestamp;
    }

    public struct InteropHistory
    {
        public Hash sourceHash;
        public string sourcePlatform;
        public string sourceChain;
        public Address sourceAddress;

        public Hash destHash;
        public string destPlatform;
        public string destChain;
        public Address destAddress;

        public string symbol;
        public BigInteger value;
    }
    
    public struct CrossChainTransfer
    {
        public CrossChainTransferStatus status;
        public string Identifier;
        public Address FromUserAddress;
        public string UserExternalAddress;
        public Address Swapper;
        public string SwapperExternalAddress;
        public string Symbol;
        public BigInteger Amount;
        public string FromPlatform;
        public string ToPlatform;
        public Hash PhantasmaHash;
        public Hash ExternalHash;
        public Timestamp StartedAt;
        public Timestamp UpdatedAt;
        public Timestamp EndedAt;
    }

    public struct PlatformTokens : ISerializable
    {
        public string Symbol;
        public int Decimals;
        public string ExternalContractAddress;
        public Address LocalContractAddress;
        public Address LocalAddress;
        public string ExternalAddress;
        
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(this.Symbol);
            writer.WriteVarInt(this.Decimals);
            writer.WriteVarString(this.ExternalContractAddress);
            writer.WriteAddress(LocalContractAddress);
            writer.WriteAddress(LocalAddress);
            writer.WriteVarString(ExternalAddress);
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Symbol = reader.ReadVarString();
            this.Decimals = (int)reader.ReadVarInt();
            this.ExternalContractAddress = reader.ReadVarString();
            this.LocalContractAddress = reader.ReadAddress();
            this.LocalAddress = reader.ReadAddress();
            this.ExternalAddress = reader.ReadVarString();
        }
    }
    
    public struct PlatformDetails : ISerializable
    {
        public string Name;
        public string MainSymbol;
        public string FuelSymbol;
        public int Decimals;
        public Address Owner;
        public Address LocalAddress;
        public string ExternalAddress;
        public bool IsSwapEnabled;
        public PlatformTokens[] Tokens;
        
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.WriteVarString(MainSymbol);
            writer.WriteVarString(FuelSymbol);
            writer.WriteVarInt(Decimals);
            writer.WriteAddress(Owner);
            writer.WriteAddress(LocalAddress);
            writer.WriteVarString(ExternalAddress);
            writer.Write(IsSwapEnabled);
            writer.WriteVarInt(Tokens.Length);
            foreach (var token in Tokens)
            {
                token.SerializeData(writer);
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            this.Name = reader.ReadVarString();
            this.MainSymbol = reader.ReadVarString();
            this.FuelSymbol = reader.ReadVarString();
            this.Decimals = (int)reader.ReadVarInt();
            this.Owner = reader.ReadAddress();
            this.LocalAddress = reader.ReadAddress();
            this.ExternalAddress = reader.ReadVarString();
            this.IsSwapEnabled = reader.ReadBoolean();
            var tokenCount = (int)reader.ReadVarInt();
            this.Tokens = new PlatformTokens[tokenCount];
            for (int i = 0; i < tokenCount; i++)
            {
                var temp = new PlatformTokens();
                temp.UnserializeData(reader);
                this.Tokens[i] = temp;
            }
        }
    }

    public sealed class InteropContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Interop;

#pragma warning disable 0649
        private StorageMap _platformHashes;
        private StorageList _withdraws;
        private StorageMap _platformsAddresses; // <Address, string>
        private StorageMap _platformsSwaps; // <Address, StorageList<InteropHistory>>
        private StorageMap _swapperTransactions; // <Address, StorageList<InteropHistory>>
        private StorageMap _crossChainTransfers; // <Address, StorageList<CrossChainTransfer>>
        private StorageMap _crossChainUserTransfers; // <Address, StorageList<CrossChainTransferHistory>>
        
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
            string externalAddress, string mainSymbol, string fuelSymbol, int decimals, bool isSwapEnabled)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(localAddress.IsInterop, "swap target must be interop address");
            
            Runtime.Expect(!HasPlatformInfo(from, platform), "platform already registered");

            var platformsForAddress = GetPlatformsForAddress(from);
            if (platformsForAddress == null)
            {
                platformsForAddress = new PlatformDetails[0];
            }

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
                Tokens = new PlatformTokens[0]
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
            string externalAddress, string mainSymbol, string fuelSymbol, int decimals, bool isSwapEnabled )
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(HasPlatformInfo(from, platform), "platform not registered");
            
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
                Tokens = platformDetails.Tokens
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
        public void RegisterTokenOnPlatform(Address from, string platform, string symbol, int decimals, string externalContractAddress, Address localContractAddress, Address localAddress, string externalAddress)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");
            
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
            
            // Emit event
            //Runtime.Notify(EventKind.Custom, from, new TokenEventData(DomainSettings.StakingTokenSymbol, stakeAmount, Runtime.Chain.Name));
        }
        
        public void RemoveTokenFromPlatform(Address from, string platform, string symbol)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");
            
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
            if ( platformInfos == null)
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
            if ( platformInfos == null)
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
        
        public PlatformDetails[] GetAllPlatforms()
        {
            return _platformsAddresses.AllValues<PlatformDetails[]>().SelectMany(p => p).ToArray();
        }

        public PlatformDetails[] GetAvailableSwappers(string platform, string symbol)
        {
            return _platformsAddresses.AllValues<PlatformDetails[]>().SelectMany(p => p).Where(p => p.Tokens.Any(t => t.Symbol == symbol) && p.Name == platform).ToArray();
        }

        public void SendTokensToPlatform(Address from, string externalAddress, string fromPlatform, string toPlatform, string symbol, BigInteger amount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");
            Runtime.Expect(fromPlatform != toPlatform, "platforms must be different");
            Runtime.Expect(fromPlatform == DomainSettings.PlatformName, "platform must be Phantasma");
            Runtime.Expect(amount > 0, "invalid amount");
            
            // Get all Platforms
            var availableSwappers = GetAvailableSwappers(toPlatform, symbol);
            Runtime.Expect(availableSwappers.Length > 0, "no available swappers");
            
            var chainTransfer = new CrossChainTransfer()
            {
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
        
        public CrossChainTransfer[] GetAllCrossChainTransfers()
        {
            return _crossChainTransfers.AllValues<StorageList>().Select(s => s.All<CrossChainTransfer>()).SelectMany(c => c).ToArray();
        }
        
        public CrossChainTransfer[] GetCrossChainTransfers()
        {
            return _crossChainTransfers.AllValues<StorageList>().Select(s => s.All<CrossChainTransfer>()).SelectMany(c => c).Where(c => c.status == CrossChainTransferStatus.Pending || c.status == CrossChainTransferStatus.InProgress).ToArray();
        }


        public void ClaimCrossChainTransferToProcess(Address from, string platform, string identifier)
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
                Runtime.Expect(crossChainTransfer.UpdatedAt.Value + 3600 > Runtime.Time, "CrossChainTransfer still processing");
            }
            crossChainTransfer.status = CrossChainTransferStatus.InProgress;
            crossChainTransfer.Swapper = from;
            crossChainTransfer.SwapperExternalAddress = platformDetail.ExternalAddress;
            crossChainTransfer.UpdatedAt = Runtime.Time;
            
            var storageList = _crossChainTransfers.Get<Address, StorageList>(from);
            var allCross = storageList.All<CrossChainTransfer>();
            var crossChainTransferIndex = allCross.ToList().FindIndex(c => c.Identifier == identifier);
            storageList.Replace(crossChainTransferIndex, crossChainTransfer);
            _crossChainTransfers.Set<Address, StorageList>(from, storageList);
        }

        public void CompleteCrossChainTransfer(Address from, string platform, string identifier, Hash hash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(HasPlatformInfo(from, platform), "No platform registered for this address");
            
            var platformDetail = GetPlatformDetailsForAddress(from, platform);
            Runtime.Expect(platformDetail.Name == platform, "platform not registered");
            var crossChainTransfers = GetCrossChainTransfers();
            var crossChainTransfer = crossChainTransfers.FirstOrDefault(c => c.Identifier == identifier);
            Runtime.Expect(crossChainTransfer.Identifier == identifier, "invalid identifier");
            
            // Validate transfer on the other chain
            bool isValid = false;
            var result = Runtime.ReadTransactionFromOracle(platform, "main", hash);
            // Validate the transaction
            
            
            Runtime.Expect(isValid, "invalid transfer");
            crossChainTransfer.status = CrossChainTransferStatus.Confirmed;
        }
        
        public void SettleCrossChainTransaction(Address from, string platform, string chain)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "must be user address");
            
            var platformDetails = GetPlatformsForAddress(from);

            if (platform != DomainSettings.PlatformName)
            {
                Runtime.Expect(HasPlatformInfo(from, platform), "unsupported platform");
                var platformInfo = GetPlatformInfoForAddress(from, platform);
            }
            else
            {
                platformDetails = null;
            }

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
                    if (entry.destination == transfer.destinationAddress && entry.transferAmount == transfer.Value && entry.transferSymbol == transfer.Symbol)
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
                        Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value), $"nft {transfer.Value} must exist");
                    }

                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

                    var withdraw = _withdraws.Get<InteropWithdraw>(index);
                    _withdraws.RemoveAt(index);

                    var org = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
                    Runtime.Expect(org.IsMember(from), $"{from.Text} is not a validator node");
                    Runtime.TransferTokens(withdraw.feeSymbol, Address, from, withdraw.feeAmount);

                    RegisterHistory(hash, withdraw.hash, DomainSettings.PlatformName, Runtime.Chain.Name, transfer.sourceAddress, hash, platform, chain, withdraw.destination, transfer.Symbol, transfer.Value);
                    swapCount++;
                }
                else if (platformDetails != null)
                {
                    foreach (var entry in platformDetails)
                    {
                        if (transfer.destinationAddress == entry.)
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
                                    Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value), $"nft {transfer.Value} must exist");
                                }


                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

                                Runtime.Expect(transfer.interopAddress.IsUser, "invalid destination address");

                                Runtime.SwapTokens(platform, transfer.sourceAddress, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value);


                                if (!token.Flags.HasFlag(TokenFlags.Fungible))
                                {
                                    var externalNft = Runtime.ReadNFTFromOracle(platform, transfer.Symbol, transfer.Value);
                                    var ram = externalNft.Serialize();

                                    var localNft = Runtime.ReadToken(transfer.Symbol, transfer.Value);
                                    Runtime.WriteToken(from, transfer.Symbol, transfer.Value, ram); // TODO "from" here might fail due to contract triggers, review this later
                                }

                                var settleHash = Runtime.Transaction.Hash;
                                RegisterHistory(settleHash, hash, platform, chain, transfer.sourceAddress, settleHash, DomainSettings.PlatformName, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value);

                                swapCount++;
                            }

                            break;
                        }
                    }
                }
            }

            Runtime.Expect(swapCount > 0, "nothing to settle");
            Runtime.Notify(EventKind.ChainSwap, from, new TransactionSettleEventData(hash, platform, chain));
        }

        public void UpdateTransactionState(Address swapAddress, Address from, string platform, string chain, Hash hash)
        {
            
        }
        #endregion

        public void SettleTransaction(Address from, string platform, string chain, Hash hash)
        {
            Runtime.Expect(Runtime.ProtocolVersion < 13, "this method is obsolete");

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
                    if (entry.destination == transfer.destinationAddress && entry.transferAmount == transfer.Value && entry.transferSymbol == transfer.Symbol)
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
                        Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value), $"nft {transfer.Value} must exist");
                    }

                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

                    var withdraw = _withdraws.Get<InteropWithdraw>(index);
                    _withdraws.RemoveAt(index);

                    var org = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
                    Runtime.Expect(org.IsMember(from), $"{from.Text} is not a validator node");
                    Runtime.TransferTokens(withdraw.feeSymbol, Address, from, withdraw.feeAmount);

                    RegisterHistory(hash, withdraw.hash, DomainSettings.PlatformName, Runtime.Chain.Name, transfer.sourceAddress, hash, platform, chain, withdraw.destination, transfer.Symbol, transfer.Value);
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
                                    Runtime.Expect(Runtime.NFTExists(transfer.Symbol, transfer.Value), $"nft {transfer.Value} must exist");
                                }


                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                                Runtime.Expect(token.Flags.HasFlag(TokenFlags.Swappable), "transfer token must be swappable");

                                Runtime.Expect(transfer.interopAddress.IsUser, "invalid destination address");

                                Runtime.SwapTokens(platform, transfer.sourceAddress, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value);


                                if (!token.Flags.HasFlag(TokenFlags.Fungible))
                                {
                                    var externalNft = Runtime.ReadNFTFromOracle(platform, transfer.Symbol, transfer.Value);
                                    var ram = externalNft.Serialize();

                                    var localNft = Runtime.ReadToken(transfer.Symbol, transfer.Value);
                                    Runtime.WriteToken(from, transfer.Symbol, transfer.Value, ram); // TODO "from" here might fail due to contract triggers, review this later
                                }

                                var settleHash = Runtime.Transaction.Hash;
                                RegisterHistory(settleHash, hash, platform, chain, transfer.sourceAddress, settleHash, DomainSettings.PlatformName, Runtime.Chain.Name, transfer.interopAddress, transfer.Symbol, transfer.Value);

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
        public void WithdrawTokens(Address from, Address to, string symbol, BigInteger value)
        {
            Runtime.Expect(Runtime.ProtocolVersion < 13, "this method is obsolete");

            Runtime.Expect(!Filter.Enabled, "swap withdraws disabled");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from.IsUser, "source must be user address");
            Runtime.Expect(to.IsInterop, "destination must be interop address");

            Runtime.Expect(Runtime.TokenExists(symbol), "invalid token");

            var transferTokenInfo = Runtime.GetToken(symbol);
            Runtime.Expect(transferTokenInfo.Flags.HasFlag(TokenFlags.Transferable), "transfer token must be transferable");
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
                Runtime.CallNativeContext(NativeContractKind.Swap, nameof(SwapContract.SwapReverse), from, DomainSettings.FuelTokenSymbol, feeSymbol, feeAmount);

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

        public Hash GetSettlement(string platformName, Hash hash)
        {
            var chainHashes = _platformHashes.Get<string, StorageMap>(platformName);
            if (chainHashes.ContainsKey(hash))
            {
                return chainHashes.Get<Hash, Hash>(hash);
            }

            return Hash.Null;
        }

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
        private void RegisterHistory(Hash swapHash, Hash sourceHash, string sourcePlatform, string sourceChain, Address sourceAddress, Hash destHash, string destPlatform, string destChain, Address destAddress, string symbol, BigInteger value)
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

        private void AppendToHistoryMap(Hash swapHash, Address target)
        {
            var list = _historyMap.Get<Address, StorageList>(target);
            list.Add(swapHash);
        }

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
    }
}
