using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;

using Phantasma.Shared;
using Phantasma.Core;
using Phantasma.Core.Context;
using Phantasma.Business;
using Phantasma.Business.Tokens;
using Phantasma.Business.Storage;
using Phantasma.Business.Contracts;
using Phantasma.Shared.Types;

using Serilog.Core;
using Foundatio.Messaging;

namespace Phantasma.Infrastructure
{
    public class APIException : Exception
    {
        public APIException(string msg) : base(msg)
        {
        }

        public APIException(string msg, Exception innerException) : base(msg, innerException)
        {
        }
    }

    public class APIDescriptionAttribute : Attribute
    {
        public readonly string Description;

        public APIDescriptionAttribute(string description)
        {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class APIFailCaseAttribute : APIDescriptionAttribute
    {
        public readonly string Value;

        public APIFailCaseAttribute(string description, string value) : base(description)
        {
            Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = true)]
    public class APIParameterAttribute : APIDescriptionAttribute
    {
        public readonly string Value;

        public APIParameterAttribute(string description, string value) : base(description)
        {
            Value = value;
        }
    }

    public class APIInfoAttribute : APIDescriptionAttribute
    {
        public readonly Type ReturnType;
        public readonly bool Paginated;
        public readonly int CacheDuration;
        public readonly string CacheTag;
        public readonly bool InternalEndpoint;

        public APIInfoAttribute(Type returnType, string description, bool paginated = false, int cacheDuration = 0, bool internalEndpoint = false, string cacheTag = null) : base(description)
        {
            ReturnType = returnType;
            Paginated = paginated;
            CacheDuration = cacheDuration;
            InternalEndpoint = internalEndpoint;
            CacheTag = cacheTag;
        }
    }

    public class NexusAPI
    {
        public readonly bool UseCache;
        public readonly Nexus Nexus;
        public ITokenSwapper TokenSwapper;
        public Mempool Mempool;

        private const int PaginationMaxResults = 99999;

        public string ProxyURL = null;

        internal readonly Logger logger;

        // NOTE - Nexus should be null only for proxy-mode
        public NexusAPI(Nexus nexus = null, bool useCache = false, Logger logger = null)
        {
            Nexus = nexus;
            UseCache = useCache;
            this.logger = logger;

            logger?.Information($"Phantasma API enabled");
        }

        public partial class Endpoints : ApiEndpointBase
        {
            private readonly IMessagePublisher _publisher;

            public Endpoints(IMessagePublisher publisher)
            {
                _publisher = publisher;
            }
        }

        #region UTILS

        private static string ExternalHashToString(string platform, Hash hash, string symbol)
        {
            var result = hash.ToString();

            switch (platform)
            {
                case "neo":
                    if (symbol == "NEO" || symbol == "GAS")
                    {
                        return result;
                    }
                    result = result.Substring(0, 40);
                    break;

                default:
                    result = result.Substring(0, 40);
                    break;
            }

            return result;
        }

        // This is required as validation to support proxy-mode
        private void RequireNexus()
        {
            if (this.Nexus == null)
            {
                throw new Exception("Nexus not available locally");
            }
        }

        private TokenResult FillToken(string tokenSymbol, bool fillSeries, bool extended)
        {
            RequireNexus();
            var tokenInfo = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);
            var currentSupply = Nexus.RootChain.GetTokenSupply(Nexus.RootChain.Storage, tokenSymbol);
            var burnedSupply = Nexus.GetBurnedTokenSupply(Nexus.RootStorage, tokenSymbol);

            var seriesList = new List<TokenSeriesResult>();

            if (!tokenInfo.IsFungible() && fillSeries)
            {
                var seriesIDs = Nexus.GetAllSeriesForToken(Nexus.RootStorage, tokenSymbol);
                //  HACK wont work if token has non-sequential series
                foreach (var ID in seriesIDs)
                {
                    var series = Nexus.GetTokenSeries(Nexus.RootStorage, tokenSymbol, ID);
                    if (series != null)
                    {
                        seriesList.Add(new TokenSeriesResult()
                        {
                            seriesID = (uint)ID,
                            currentSupply = series.MintCount.ToString(),
                            maxSupply = series.MaxSupply.ToString(),
                            burnedSupply = Nexus.GetBurnedTokenSupplyForSeries(Nexus.RootStorage, tokenSymbol, ID).ToString(),
                            mode = series.Mode,
                            script = Base16.Encode(series.Script),
                            methods = extended ? FillMethods(series.ABI.Methods) : new ABIMethodResult[0]
                        }); ;
                    }
                }
            }

            var external = new List<TokenExternalResult>();

            if (tokenInfo.Flags.HasFlag(TokenFlags.Swappable))
            {
                var platforms = this.Nexus.GetPlatforms(Nexus.RootStorage);
                foreach (var platform in platforms)
                {
                    var extHash = this.Nexus.GetTokenPlatformHash(tokenSymbol, platform, Nexus.RootStorage);
                    if (!extHash.IsNull)
                    {
                        external.Add(new TokenExternalResult()
                        {   
                            hash = ExternalHashToString(platform, extHash, tokenSymbol),
                            platform = platform,
                        });
                    }
                }
            }

            var prices = new List<TokenPriceResult>();

            if (extended)
            {
                for (int i=0; i<30; i++)
                {
                    prices.Add(new TokenPriceResult()
                    {
                        Open = "0",
                        Close = "0",
                        High = "0",
                        Low = "0",
                    });
                }
            }

            return new TokenResult
            {
                symbol = tokenInfo.Symbol,
                name = tokenInfo.Name,
                currentSupply = currentSupply.ToString(),
                maxSupply = tokenInfo.MaxSupply.ToString(),
                burnedSupply = burnedSupply.ToString(),
                decimals = tokenInfo.Decimals,
                flags = tokenInfo.Flags.ToString(),//.Split(',').Select(x => x.Trim()).ToArray(),
                address = SmartContract.GetAddressForName(tokenInfo.Symbol).Text,
                owner = tokenInfo.Owner.Text,
                script = tokenInfo.Script.Encode(),
                series = seriesList.ToArray(),
                external = external.ToArray(),
                price = prices.ToArray(),
            };
        }

        private TokenDataResult FillNFT(string symbol, BigInteger ID, bool extended)
        {
            RequireNexus();

            TokenContent info = Nexus.ReadNFT(Nexus.RootStorage, symbol, ID);

            var properties = new List<TokenPropertyResult>();
            if (extended)
            {
                var chain = FindChainByInput("main");
                var series = Nexus.GetTokenSeries(Nexus.RootStorage, symbol, info.SeriesID);
                if (series != null)
                {
                    foreach (var method in series.ABI.Methods)
                    {
                        if (method.IsProperty())
                        {
                            if (symbol == DomainSettings.RewardTokenSymbol && method.name == "getImageURL")
                            {
                                properties.Add(new TokenPropertyResult() { Key = "ImageURL", Value = "https://phantasma.io/img/crown.png" });
                            }
                            else
                            if (symbol == DomainSettings.RewardTokenSymbol && method.name == "getInfoURL")
                            {
                                properties.Add(new TokenPropertyResult() { Key = "InfoURL", Value = "https://phantasma.io/crown/" + ID });
                            }
                            else
                            if (symbol == DomainSettings.RewardTokenSymbol && method.name == "getName")
                            {
                                properties.Add(new TokenPropertyResult() { Key = "Name", Value = "Crown #" + info.MintID });
                            }
                            else
                            {
                                Business.Tokens.TokenUtils.FetchProperty(Nexus.RootStorage, chain, method.name, series, ID, (propName, propValue) =>
                                {
                                    string temp;
                                    if (propValue.Type == VMType.Bytes)
                                    {
                                        temp = "0x" + Base16.Encode(propValue.AsByteArray());
                                    }
                                    else
                                    {
                                        temp = propValue.AsString();
                                    }

                                    properties.Add(new TokenPropertyResult() { Key = propName, Value = temp });
                                });
                            }
                        }
                    }
                }
            }

            var infusion = info.Infusion.Select(x => new TokenPropertyResult() { Key = x.Symbol, Value = x.Value.ToString() }).ToArray();

            var result = new TokenDataResult()
            {
                chainName = info.CurrentChain,
                creatorAddress = info.Creator.Text,
                ownerAddress = info.CurrentOwner.Text,
                series = info.SeriesID.ToString(),
                mint = info.MintID.ToString(),
                ID = ID.ToString(),
                rom = Base16.Encode(info.ROM),
                ram = Base16.Encode(info.RAM),
                status = info.CurrentOwner == DomainSettings.InfusionAddress ? "infused" : "active",
                infusion = infusion,
                properties = properties.ToArray()
            };

            return result;
        }

        private AuctionResult FillAuction(MarketAuction auction, Chain chain)
        {
            RequireNexus();

            var nft = Nexus.ReadNFT(Nexus.RootStorage, auction.BaseSymbol, auction.TokenID);

            return new AuctionResult
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

        private TransactionResult FillTransaction(Transaction tx)
        {
            RequireNexus();

            var block = Nexus.FindBlockByTransaction(tx);
            var chain = block != null ? Nexus.GetChainByAddress(block.ChainAddress) : null;

            var result = new TransactionResult
            {
                hash = tx.Hash.ToString(),
                chainAddress = chain != null ? chain.Address.Text : Address.Null.Text,
                timestamp = block != null ? block.Timestamp.Value : 0,
                blockHeight = block != null ? (int)block.Height : -1,
                blockHash = block != null ? block.Hash.ToString() : Hash.Null.ToString(),
                script = tx.Script.Encode(),
                payload = tx.Payload.Encode(),
                fee = chain != null ? chain.GetTransactionFee(tx.Hash).ToString() : "0",
                expiration = tx.Expiration.Value,
                signatures = tx.Signatures.Select(x => new SignatureResult() { Kind = x.Kind.ToString(), Data = Base16.Encode(x.ToByteArray()) }).ToArray(),
            };

            if (block != null)
            {
                var eventList = new List<EventResult>();

                var evts = block.GetEventsForTransaction(tx.Hash);
                foreach (var evt in evts)
                {
                    var eventEntry = FillEvent(evt);
                    eventList.Add(eventEntry);
                }

                var txResult = block.GetResultForTransaction(tx.Hash);
                result.result = txResult != null ? Base16.Encode(txResult) : "";
                result.events = eventList.ToArray();
            }
            else
            {
                result.result = "";
                result.events = new EventResult[0];
            }

            return result;
        }

        private EventResult FillEvent(Event evt)
        {
            return new EventResult
            {
                address = evt.Address.Text,
                contract = evt.Contract,
                data = evt.Data.Encode(),
                kind = evt.Kind >= EventKind.Custom ? ((byte)evt.Kind).ToString() : evt.Kind.ToString()
            };
        }

        private OracleResult FillOracle(IOracleEntry oracle)
        {
            return new OracleResult
            {
                url = oracle.URL,
                content = (oracle.Content.GetType() == typeof(byte[]))
                    ? Base16.Encode(oracle.Content as byte[])
                    : Base16.Encode(Serialization.Serialize(oracle.Content))
            };
        }

        private BlockResult FillBlock(Block block, Chain chain)
        {
            RequireNexus();

            var result = new BlockResult
            {
                hash = block.Hash.ToString(),
                previousHash = block.PreviousHash.ToString(),
                timestamp = block.Timestamp.Value,
                height = (uint)block.Height,
                chainAddress = chain.Address.ToString(),
                protocol = block.Protocol,
                reward = chain.GetBlockReward(block).ToString(),
                validatorAddress = block.Validator.ToString(),
                events = block.Events.Select(x => FillEvent(x)).ToArray(),
                oracles = block.OracleData.Select(x => FillOracle(x)).ToArray(),
            };

            var txs = new List<TransactionResult>();
            if (block.TransactionHashes != null && block.TransactionHashes.Any())
            {
                foreach (var transactionHash in block.TransactionHashes)
                {
                    var tx = Nexus.FindTransactionByHash(transactionHash);
                    var txEntry = FillTransaction(tx);
                    txs.Add(txEntry);
                }
            }
            result.txs = txs.ToArray();

            // todo add other block info, eg: size, gas, txs
            return result;
        }

        private ChainResult FillChain(Chain chain)
        {
            RequireNexus();

            Throw.IfNull(chain, nameof(chain));

            var parentName = Nexus.GetParentChainByName(chain.Name);
            var orgName = Nexus.GetChainOrganization(chain.Name);

            var contracts = chain.GetContracts(chain.Storage).ToArray();

            var result = new ChainResult
            {
                name = chain.Name,
                address = chain.Address.Text,
                height = (uint)chain.Height,
                parent = parentName,
                organization = orgName,
                contracts = contracts.Select(x => x.Name).ToArray(),
                dapps = new string[0],
            };

            return result;
        }

        private Chain FindChainByInput(string chainInput)
        {
            RequireNexus();

            var chain = Nexus.GetChainByName(chainInput);

            if (chain != null)
            {
                return chain;
            }

            if (Address.IsValidAddress(chainInput))
            {
                return Nexus.GetChainByAddress(Address.FromText(chainInput));
            }

            return null;
        }

        private ABIMethodResult[] FillMethods(IEnumerable<ContractMethod> methods)
        {
            return methods.Select(x => new ABIMethodResult()
            {
                name = x.name,
                returnType = x.returnType.ToString(),
                parameters = x.parameters.Select(y => new ABIParameterResult()
                {
                    name = y.name,
                    type = y.type.ToString()
                }).ToArray()
            }).ToArray();
        }

        private ContractResult FillContract(string name, SmartContract contract)
        {
            var customContract = contract as CustomContract;
            var scriptBytes = customContract != null ? customContract.Script : new byte[0];

            return new ContractResult
            {
                name = name,
                script = Base16.Encode(scriptBytes),
                address = contract.Address.Text,
                methods = FillMethods(contract.ABI.Methods),
                events = contract.ABI.Events.Select(x => new ABIEventResult()
                {
                    name = x.name,
                    returnType = x.returnType.ToString(),
                    value = x.value,
                    description = Base16.Encode(x.description),
                }).ToArray()
            };
        }

        private ReceiptResult FillReceipt(RelayReceipt receipt)
        {
            return new ReceiptResult()
            {
                nexus = receipt.message.nexus,
                index = receipt.message.index.ToString(),
                timestamp = receipt.message.timestamp.Value,
                sender = receipt.message.sender.Text,
                receiver = receipt.message.receiver.Text,
                script = Base16.Encode(receipt.message.script ?? new byte[0])
            };
        }

        private ArchiveResult FillArchive(Archive archive)
        {
            return new ArchiveResult()
            {
                hash = archive.Hash.ToString(),
                name = archive.Name,
                time = archive.Time.Value,
                size = (uint)archive.Size,
                encryption = Base16.Encode(archive.Encryption.ToBytes()),
                blockCount = (int)archive.BlockCount,
                missingBlocks = archive.MissingBlockIndices.ToArray(),
                owners = archive.Owners.Select(x => x.Text).ToArray()
            };
        }

        private StorageResult FillStorage(Address address)
        {
            RequireNexus();

            var storage = new StorageResult();

            storage.used = (uint)Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "storage", nameof(StorageContract.GetUsedSpace), address).AsNumber();
            storage.available = (uint)Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "storage", nameof(StorageContract.GetAvailableSpace), address).AsNumber();

            if (storage.used > 0)
            {
                var files = (Hash[])Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "storage", nameof(StorageContract.GetFiles), address).ToObject();

                Hash avatarHash = Hash.Null;
                storage.archives = files.Select(x => {
                    var result = FillArchive(Nexus.GetArchive(Nexus.RootStorage, x));

                    if (result.name == "avatar")
                    {
                        avatarHash = x;
                    }

                    return result;
                }).ToArray();

                if (avatarHash != Hash.Null)
                {
                    var avatarArchive = Nexus.GetArchive(Nexus.RootStorage, avatarHash);

                    var avatarData = Nexus.ReadArchiveBlock(avatarArchive, 0);
                    if (avatarData != null && avatarData.Length > 0)
                    {
                        storage.avatar = Encoding.ASCII.GetString(avatarData);
                    }
                }
            }
            else
            {
                storage.archives = new ArchiveResult[0];
            }

            if (storage.avatar == null)
            {
                storage.avatar = DefaultAvatar.Data;
            }

            return storage;
        }
        #endregion

        [APIInfo(typeof(AccountResult), "Returns the account name and balance of given address.", false, 10)]
        [APIFailCase("address is invalid", "ABCD123")]
        public AccountResult GetAccount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account)
        {
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            var address = Address.FromText(account);

            AccountResult result;

            try
            {
                result = FillAccount(address);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            return result;
        }

        [APIInfo(typeof(AccountResult[]), "Returns data about several accounts.", false, 10)]
        public AccountResult[] GetAccounts([APIParameter("Multiple addresses separated by comma", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV,PDHcFHq2femFnj2ZaFc7iu3qD4XjZG9eVZXuwDrtJGDhj")] string accountText)
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
                    result = FillAccount(address);
                    list.Add(result);
                }
                catch (Exception e)
                {
                    throw new APIException(e.Message);
                }
            }

            return list.ToArray();
        }

        private AccountResult FillAccount(Address address)
        {
            RequireNexus();

            var result = new AccountResult();
            result.address = address.Text;
            result.name = Nexus.RootChain.GetNameFromAddress(Nexus.RootStorage, address);

            var stake = Nexus.GetStakeFromAddress(Nexus.RootStorage, address);

            if (stake > 0)
            {
                var unclaimed = Nexus.GetUnclaimedFuelFromAddress(Nexus.RootStorage, address);
                var time = Nexus.GetStakeTimestampOfAddress(Nexus.RootStorage, address);
                result.stakes = new StakeResult() { amount = stake.ToString(), time = time.Value, unclaimed = unclaimed.ToString() };
            }
            else
            {
                result.stakes = new StakeResult() { amount = "0", time = 0, unclaimed = "0" };
            }

            result.storage = FillStorage(address);

            // deprecated
            result.stake = result.stakes.amount;
            result.unclaimed = result.stakes.unclaimed;

            var validator = Nexus.GetValidatorType(address);

            var balanceList = new List<BalanceResult>();
            var symbols = Nexus.GetTokens(Nexus.RootStorage);
            var chains = Nexus.GetChains(Nexus.RootStorage);
            foreach (var symbol in symbols)
            {
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var token = Nexus.GetTokenInfo(Nexus.RootStorage, symbol);
                    var balance = chain.GetTokenBalance(chain.Storage, token, address);
                    if (balance > 0)
                    {
                        var balanceEntry = new BalanceResult
                        {
                            chain = chain.Name,
                            amount = balance.ToString(),
                            symbol = token.Symbol,
                            decimals = (uint)token.Decimals,
                            ids = new string[0]
                        };

                        if (!token.IsFungible())
                        {
                            var ownerships = new OwnershipSheet(symbol);
                            var idList = ownerships.Get(chain.Storage, address);
                            if (idList != null && idList.Any())
                            {
                                balanceEntry.ids = idList.Select(x => x.ToString()).ToArray();
                            }
                        }
                        balanceList.Add(balanceEntry);
                    }
                }
            }

            result.relay = Nexus.GetRelayBalance(address).ToString();
            result.balances = balanceList.ToArray();
            result.validator = validator.ToString();

            result.txs = Nexus.RootChain.GetTransactionHashesForAddress(address).Select(x => x.ToString()).ToArray();

            return result;
        }

        [APIInfo(typeof(string), "Returns the address that owns a given name.", false, 60)]
        [APIFailCase("address is invalid", "ABCD123")]
        public string LookUpName([APIParameter("Name of account", "blabla")] string name)
        {
            if (!ValidationUtils.IsValidIdentifier(name))
            {
                throw new APIException("invalid name");
            }

            RequireNexus();

            var address = Nexus.LookUpName(Nexus.RootStorage, name);
            if (address.IsNull)
            {
                throw new APIException("name not owned");
            }

            return address.Text;
        }

        [APIInfo(typeof(int), "Returns the height of a chain.", false, 3)]
        [APIFailCase("chain is invalid", "4533")]
        public BigInteger GetBlockHeight([APIParameter("Address or name of chain", "root")] string chainInput)
        {
            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("invalid chain");
            }

            return chain.Height;
        }

        [APIInfo(typeof(int), "Returns the number of transactions of given block hash or error if given hash is invalid or is not found.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        public int GetBlockTransactionCountByHash([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName, [APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }


            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = chain.GetBlockByHash(hash);

                if (block != null)
                {
                    int count = block.TransactionHashes.Count();

                    return count;
                }
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by hash.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        public BlockResult GetBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                RequireNexus();
                var chains = Nexus.GetChains(Nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var block = chain.GetBlockByHash(hash);
                    if (block != null)
                    {
                        return FillBlock(block, chain);
                    }
                }
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(string), "Returns a serialized string, containing information about a block by hash.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        public string GetRawBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                RequireNexus();
                var chains = Nexus.GetChains(Nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var block = chain.GetBlockByHash(hash);
                    if (block != null)
                    {
                        return block.ToByteArray(true).Encode();
                    }
                }
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by height and chain.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        public BlockResult GetBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] uint height)
        {
            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("chain not found");
            }

            var blockHash = chain.GetBlockHashAtHeight(height);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return FillBlock(block, chain);
            }

            throw new APIException("block not found");
        }

        [APIInfo(typeof(string), "Returns a serialized string, in hex format, containing information about a block by height and chain.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        public string GetRawBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] uint height)
        {
            RequireNexus();
            var chain = Nexus.GetChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    throw new APIException("chain not found");
                }
                chain = Nexus.GetChainByAddress(Address.FromText(chainInput));
            }

            if (chain == null)
            {
                throw new APIException("chain not found");
            }

            var blockHash = chain.GetBlockHashAtHeight(height);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return block.ToByteArray(true).Encode();
            }

            throw new APIException("block not found");
        }

        [APIInfo(typeof(TransactionResult), "Returns the information about a transaction requested by a block hash and transaction index.", false, -1)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("index transaction is invalid", "-1")]
        public TransactionResult GetTransactionByBlockHashAndIndex([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName, [APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash, [APIParameter("Index of transaction", "0")] int index)
        {
            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = chain.GetBlockByHash(hash);

                if (block == null)
                {
                    throw new APIException("unknown block hash");
                }

                if (index < 0 || index >= block.TransactionCount)
                {
                    throw new APIException("invalid transaction index");
                }

                var txHash = block.TransactionHashes.ElementAtOrDefault(index);

                if (txHash == Hash.Null)
                {
                    throw new APIException("unknown tx index");
                }

                return FillTransaction(Nexus.FindTransactionByHash(txHash));
            }

            throw new APIException("invalid block hash");
        }

        [APIInfo(typeof(AccountTransactionsResult), "Returns last X transactions of given address.", true, 3)]
        [APIFailCase("address is invalid", "543533")]
        [APIFailCase("page is invalid", "-1")]
        [APIFailCase("pageSize is invalid", "-1")]
        public PaginatedResult GetAddressTransactions([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Index of page to return", "5")] uint page = 1, [APIParameter("Number of items to return per page", "5")] uint pageSize = PaginationMaxResults)
        {
            if (page < 1 || pageSize < 1)
            {
                throw new APIException("invalid page/pageSize");
            }

            if (pageSize > PaginationMaxResults)
            {
                pageSize = PaginationMaxResults;
            }

            if (Address.IsValidAddress(account))
            {
                RequireNexus();

                var paginatedResult = new PaginatedResult();
                var address = Address.FromText(account);

                var chain = Nexus.RootChain;
                // pagination
                var txHashes = chain.GetTransactionHashesForAddress(address);
                uint numberRecords = (uint)txHashes.Length;
                uint totalPages = (uint)Math.Ceiling(numberRecords / (double)pageSize);
                //

                var txs = txHashes.Select(x => chain.GetTransactionByHash(x))
                    .OrderByDescending(tx => Nexus.FindBlockByTransaction(tx).Timestamp.Value)
                    .Skip((int)((page - 1) * pageSize))
                    .Take((int)pageSize);

                var result = new AccountTransactionsResult
                {
                    address = address.Text,
                    txs = txs.Select(FillTransaction).ToArray()
                };

                paginatedResult.pageSize = pageSize;
                paginatedResult.totalPages = totalPages;
                paginatedResult.total = numberRecords;
                paginatedResult.page = page;

                paginatedResult.result = result;

                return paginatedResult;
            }
            else
            {
                throw new APIException("invalid address");
            }
        }

        [APIInfo(typeof(int), "Get number of transactions in a specific address and chain", false, 3)]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("chain is invalid", "-1")]
        public int GetAddressTransactionCount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Name or address of chain, optional", "apps")] string chainInput = "main")
        {
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            var address = Address.FromText(account);

            int count = 0;

            if (!string.IsNullOrEmpty(chainInput))
            {
                var chain = FindChainByInput(chainInput);
                if (chain == null)
                {
                    throw new APIException("invalid chain");
                }

                var txHashes = chain.GetTransactionHashesForAddress(address);
                count = txHashes.Length;
            }
            else
            {
                RequireNexus();

                var chains = Nexus.GetChains(Nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var txHashes = chain.GetTransactionHashesForAddress(address);
                    count += txHashes.Length;
                }
            }

            return count;
        }

        [APIInfo(typeof(string), "Allows to broadcast a signed operation on the network, but it's required to build it manually.", false, 0, true)]
        [APIFailCase("rejected by mempool", "0000")] // TODO not correct
        [APIFailCase("script is invalid", "")]
        [APIFailCase("failed to decoded transaction", "0000")]
        public string SendRawTransaction([APIParameter("Serialized transaction bytes, in hexadecimal format", "0000000000")] string txData)
        {
            if (Mempool == null)
            {
                throw new APIException("Node not accepting transactions");
            }

            byte[] bytes;
            try
            {
                bytes = Base16.Decode(txData);
            }
            catch
            {
                throw new APIException("Failed to decode script");
            }

            if (bytes.Length == 0)
            {
                throw new APIException("Invalid transaction script");
            }

            var tx = Transaction.Unserialize(bytes);
            if (tx == null)
            {
                throw new APIException("Failed to deserialize transaction");
            }

            try
            {
                Mempool.Submit(tx);
            }
            catch (MempoolSubmissionException e)
            {
                var errorMessage = "Mempool submission rejected: " + e.Message;
                logger?.Warning(errorMessage);
                throw new APIException(errorMessage);
            }
            catch (Exception)
            {
                throw new APIException("Mempool submission rejected: internal error");
            }

            return tx.Hash.ToString();
        }

        [APIInfo(typeof(ScriptResult), "Allows to invoke script based on network state, without state changes.", false, 5, true)]
        [APIFailCase("script is invalid", "")]
        [APIFailCase("failed to decoded script", "0000")]
        public ScriptResult InvokeRawScript([APIParameter("Address or name of chain", "root")] string chainInput, [APIParameter("Serialized script bytes, in hexadecimal format", "0000000000")] string scriptData)
        {
            var chain = FindChainByInput(chainInput);
            if (chain == null)
            {
                throw new APIException("invalid chain");
            }

            byte[] script;
            try
            {
                script = Base16.Decode(scriptData);
            }
            catch
            {
                throw new APIException("Failed to decode script");
            }

            if (script.Length == 0)
            {
                throw new APIException("Invalid transaction script");
            }

            //System.IO.File.AppendAllLines(@"c:\code\bug_vm.txt", new []{string.Join("\n", new VM.Disassembler(script).Instructions)});

            RequireNexus();

            var changeSet = new StorageChangeSetContext(chain.Storage);
            var oracle = Nexus.GetOracleReader();
            uint offset = 0;
            var vm = new RuntimeVM(-1, script, offset, chain, Address.Null, Timestamp.Now, null, changeSet, oracle, ChainTask.Null, true);

            string error = null;
            ExecutionState state = ExecutionState.Fault;
            try
            {
                state = vm.Execute();
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            if (error != null)
            {
                throw new APIException($"Execution failed: {error}");
            }

            var results = new Stack<string>();

            while (vm.Stack.Count > 0)
            {
                var result = vm.Stack.Pop();

                if (result.Type == VMType.Object)
                {
                    result = VMObject.CastTo(result, VMType.Struct);
                }

                var resultBytes = Serialization.Serialize(result);
                results.Push(Base16.Encode(resultBytes));
            }

            var evts = vm.Events.Select(evt => new EventResult() { address = evt.Address.Text, kind = evt.Kind.ToString(), data = Base16.Encode(evt.Data) }).ToArray();

            var oracleReads = oracle.Entries.Select(x => new OracleResult()
            {
                url = x.URL,
                content = Base16.Encode((x.Content.GetType() == typeof(byte[]) ? x.Content as byte[] : Serialization.Serialize(x.Content)))
            }).ToArray();

            var resultArray = results.ToArray();
            return new ScriptResult { results = resultArray, result = resultArray.FirstOrDefault(), events = evts, oracles = oracleReads };
        }

        [APIInfo(typeof(TransactionResult), "Returns information about a transaction by hash.", false, -1, true)]
        [APIFailCase("hash is invalid", "43242342")]
        public TransactionResult GetTransaction([APIParameter("Hash of transaction", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;
            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("Invalid hash");
            }

            RequireNexus();
            var tx = Nexus.FindTransactionByHash(hash);

            if (tx == null)
            {
                if (Mempool != null)
                {
                    var status = Mempool.GetTransactionStatus(hash, out string reason);
                    switch (status)
                    {
                        case MempoolTransactionStatus.Pending:
                            throw new APIException("pending");

                        case MempoolTransactionStatus.Rejected:
                            throw new APIException("rejected: " + reason);
                    }
                }

                throw new APIException("Transaction not found");
            }

            return FillTransaction(tx);
        }

        [APIInfo(typeof(string), "Removes a pending transaction from the mempool.")]
        [APIFailCase("hash is invalid", "43242342")]
        public string CancelTransaction([APIParameter("Hash of transaction", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            if (Mempool == null)
            {
                throw new APIException("mempool not available");
            }

            Hash hash;
            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("Invalid hash");
            }

            RequireNexus();
            var tx = Nexus.FindTransactionByHash(hash);

            if (tx != null)
            {
                throw new APIException("already in chain");
            }

            var status = Mempool.GetTransactionStatus(hash, out string reason);
            if (status == MempoolTransactionStatus.Pending)
            {
                if (Mempool.Discard(hash))
                {
                    return hash.ToString();
                }
            }

            throw new APIException("Transaction not found");
        }

        [APIInfo(typeof(ChainResult[]), "Returns an array of all chains deployed in Phantasma.", false, 300)]
        public ChainResult[] GetChains()
        {
            var objs = new List<ChainResult>();

            RequireNexus();
            var chains = Nexus.GetChains(Nexus.RootStorage);
            foreach (var chainName in chains)
            {
                var chain = Nexus.GetChainByName(chainName);
                var single = FillChain(chain);
                objs.Add(single);
            }

            return objs.ToArray();
        }

        [APIInfo(typeof(NexusResult), "Returns info about the nexus.", false, 60)]
        public NexusResult GetNexus(bool extended = false)
        {
            RequireNexus();

            var tokenList = new List<TokenResult>();

            var symbols = Nexus.GetTokens(Nexus.RootStorage);
            foreach (var token in symbols)
            {
                var entry = FillToken(token, false, extended);
                tokenList.Add(entry);
            }

            var platformList = new List<PlatformResult>();

            var platforms = Nexus.GetPlatforms(Nexus.RootStorage);
            foreach (var platform in platforms)
            {
                var info = Nexus.GetPlatformInfo(Nexus.RootStorage, platform);

                var entry = new PlatformResult();
                entry.platform = platform;
                entry.interop = info.InteropAddresses.Select(x => new InteropResult()
                {
                    local = x.LocalAddress.Text,
                    external = x.ExternalAddress
                }).ToArray();
                entry.chain = DomainExtensions.GetChainAddress(info).Text;
                entry.fuel = info.Symbol;
                entry.tokens = symbols.Where(x => Nexus.HasTokenPlatformHash(x, platform, Nexus.RootStorage)).ToArray();
                platformList.Add(entry);
            }

            var chainList = new List<ChainResult>();

            var chains = Nexus.GetChains(Nexus.RootStorage);
            foreach (var chainName in chains)
            {
                var chain = Nexus.GetChainByName(chainName);
                var single = FillChain(chain);
                chainList.Add(single);
            }

            var governance = (GovernancePair[])Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "governance", nameof(GovernanceContract.GetValues)).ToObject();

            var orgs = Nexus.GetOrganizations(Nexus.RootStorage);

            return new NexusResult()
            {
                name = Nexus.Name,
                protocol = Nexus.GetProtocolVersion(Nexus.RootStorage),
                tokens = tokenList.ToArray(),
                platforms = platformList.ToArray(),
                chains = chainList.ToArray(),
                organizations = orgs,
                governance = governance.Select(x => new GovernanceResult() { name = x.Name, value = x.Value.ToString() }).ToArray()
            };
        }

        [APIInfo(typeof(OrganizationResult), "Returns info about an organization.", false, 60)]
        public OrganizationResult GetOrganization(string ID)
        {
            RequireNexus();

            if (!Nexus.OrganizationExists(Nexus.RootStorage, ID))
            {
                throw new APIException("invalid organization");
            }

            var org = Nexus.GetOrganizationByName(Nexus.RootStorage, ID);
            var members = org.GetMembers();

            return new OrganizationResult()
            {
                id = ID,
                name = org.Name,
                members = members.Select(x => x.Text).ToArray(),
            };
        }

        [APIInfo(typeof(LeaderboardResult), "Returns content of a Phantasma leaderboard.", false, 30)]
        public LeaderboardResult GetLeaderboard(string name)
        {
            RequireNexus();
            var temp = Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "ranking", nameof(RankingContract.GetRows), name).ToObject();

            try
            {
                var board = ((LeaderboardRow[])temp).ToArray();

                return new LeaderboardResult()
                {
                    name = name,
                    rows = board.Select(x => new LeaderboardRowResult() { address = x.address.Text, value = x.score.ToString() }).ToArray(),
                };
            }
            catch (Exception e)
            {
                throw new APIException($"error fetching leaderboard: {e.Message}");
            }

        }

        [APIInfo(typeof(TokenResult[]), "Returns an array of tokens deployed in Phantasma.", false, 300)]
        public TokenResult[] GetTokens(bool extended = false)
        {
            RequireNexus();

            var tokenList = new List<TokenResult>();

            var symbols = Nexus.GetTokens(Nexus.RootStorage);
            foreach (var token in symbols)
            {
                var entry = FillToken(token, false, extended);
                tokenList.Add(entry);
            }

            return tokenList.ToArray();
        }

        [APIInfo(typeof(TokenResult), "Returns info about a specific token deployed in Phantasma.", false, 120)]
        public TokenResult GetToken([APIParameter("Token symbol to obtain info", "SOUL")] string symbol, bool extended = false)
        {
            RequireNexus();

            if (!Nexus.TokenExists(Nexus.RootStorage, symbol))
            {
                throw new APIException("invalid token");
            }

            var result = FillToken(symbol, true, extended);

            return result;
        }


        // deprecated
        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.", false, 15)]
        public TokenDataResult GetTokenData([APIParameter("Symbol of token", "NACHO")] string symbol, [APIParameter("ID of token", "1")] string IDtext)
        {
            return GetNFT(symbol, IDtext, false);
        }


        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.", false, 15)]
        public TokenDataResult GetNFT([APIParameter("Symbol of token", "NACHO")] string symbol, [APIParameter("ID of token", "1")] string IDtext, bool extended = false)
        {
            RequireNexus();

            if (!Nexus.TokenExists(Nexus.RootStorage, symbol))
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
                result = FillNFT(symbol, ID, extended);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            return result;
        }


        [APIInfo(typeof(TokenDataResult[]), "Returns an array of NFTs.", false, 300)]
        public TokenDataResult[] GetNFTs([APIParameter("Symbol of token", "NACHO")] string symbol, [APIParameter("Multiple IDs of token, separated by comman", "1")] string IDText, bool extended = false)
        {
            RequireNexus();

            if (!Nexus.TokenExists(Nexus.RootStorage, symbol))
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

                    var result = FillNFT(symbol, ID, extended);

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
        public BalanceResult GetTokenBalance([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Token symbol", "SOUL")] string tokenSymbol, [APIParameter("Address or name of chain", "root")] string chainInput)
        {
            if (!Address.IsValidAddress(account))
            {
                throw new APIException("invalid address");
            }

            RequireNexus();

            if (!Nexus.TokenExists(Nexus.RootStorage, tokenSymbol))
            {
                throw new APIException("invalid token");
            }

            var tokenInfo = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);

            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                throw new APIException("invalid chain");
            }

            var address = Address.FromText(account);
            var token = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);
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

        [APIInfo(typeof(int), "Returns the number of active auctions.", false, 30)]
        public int GetAuctionsCount([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName = null, [APIParameter("Token symbol used as filter", "NACHO")]
            string symbol = null)
        {
            RequireNexus();

            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
            {
                throw new APIException("Market not available");
            }

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContract(chain.Storage, "market", "GetAuctions").ToObject();

            if (!string.IsNullOrEmpty(symbol))
            {
                entries = entries.Where(x => x.BaseSymbol == symbol);
            }

            return entries.Count();
        }

        [APIInfo(typeof(AuctionResult[]), "Returns the auctions available in the market.", true, 30)]
        public PaginatedResult GetAuctions([APIParameter("Chain address or name where the market is located", "NACHO")] string chainAddressOrName, [APIParameter("Token symbol used as filter", "NACHO")] string symbol = null,
            [APIParameter("Index of page to return", "5")] uint page = 1,
            [APIParameter("Number of items to return per page", "5")] uint pageSize = PaginationMaxResults)
        {
            RequireNexus();

            var chain = FindChainByInput(chainAddressOrName);
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

            if (pageSize > PaginationMaxResults)
            {
                pageSize = PaginationMaxResults;
            }

            var paginatedResult = new PaginatedResult();

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContract(chain.Storage, "market", "GetAuctions").ToObject();

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

            paginatedResult.result = entries.Select(x => FillAuction(x, chain)).ToArray();

            return paginatedResult;
        }

        [APIInfo(typeof(AuctionResult), "Returns the auction for a specific token.", false, 30)]
        public AuctionResult GetAuction([APIParameter("Chain address or name where the market is located", "NACHO")] string chainAddressOrName, [APIParameter("Token symbol", "NACHO")] string symbol, [APIParameter("Token ID", "1")] string IDtext)
        {
            RequireNexus();

            if (!Nexus.TokenExists(Nexus.RootStorage, symbol))
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
                var info = Nexus.ReadNFT(Nexus.RootStorage, symbol, ID);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }

            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
            {
                throw new APIException("Market not available");
            }

            var nft = Nexus.ReadNFT(Nexus.RootStorage, symbol, ID);

            var forSale = chain.InvokeContract(chain.Storage, "market", "HasAuction", symbol, ID).AsBool();
            if (!forSale)
            {
                throw new APIException("Token not for sale");
            }

            var auction = (MarketAuction)chain.InvokeContract(chain.Storage, "market", "GetAuction", symbol, ID).ToObject();

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

        [APIInfo(typeof(ArchiveResult), "Returns info about a specific archive.", false, 300, true)]
        public ArchiveResult GetArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            RequireNexus();

            var archive = Nexus.GetArchive(Nexus.RootStorage, hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }

            return FillArchive(archive);
        }

        [APIInfo(typeof(bool), "Writes the contents of an incomplete archive.", false, 0, true)]
        public bool WriteArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, [APIParameter("Block index, starting from 0", "0")] int blockIndex, [APIParameter("Block content bytes, in Base64", "QmFzZTY0IGVuY29kZWQgdGV4dA==")] string blockContent)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            RequireNexus();

            var archive = Nexus.GetArchive(Nexus.RootStorage, hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }

            if (blockIndex < 0 || blockIndex >= archive.BlockCount)
            {
                throw new APIException("invalid block index");
            }

            var bytes = Convert.FromBase64String(blockContent);

            try
            {
                Nexus.WriteArchiveBlock(archive, blockIndex, bytes);
                return true;
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }

        [APIInfo(typeof(string), "Reads given archive block.", false, 0, true)]
        public string ReadArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, [APIParameter("Block index, starting from 0", "0")] int blockIndex)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                throw new APIException("invalid hash");
            }

            RequireNexus();

            var archive = Nexus.GetArchive(Nexus.RootStorage, hash);
            if (archive == null)
            {
                throw new APIException("archive not found");
            }

            if (blockIndex < 0 || blockIndex >= archive.BlockCount)
            {
                throw new APIException("invalid block index");
            }

            try
            {
                var bytes = Nexus.ReadArchiveBlock(archive, blockIndex);
                return Convert.ToBase64String(bytes);
            }
            catch (Exception e)
            {
                throw new APIException(e.Message);
            }
        }

        [APIInfo(typeof(ContractResult), "Returns the ABI interface of specific contract.", false, 300)]
        public ContractResult GetContract([APIParameter("Chain address or name where the contract is deployed", "main")] string chainAddressOrName, [APIParameter("Contract name", "account")] string contractName)
        {
            RequireNexus();

            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                throw new APIException("Chain not found");
            }

            if (string.IsNullOrEmpty(contractName))
            {
                throw new APIException("Invalid contract name");
            }

            if (!chain.IsContractDeployed(chain.Storage, contractName))
            {
                throw new APIException("Contract not found");
            }

            var contract = chain.GetContractByName(chain.Storage, contractName);
            return FillContract(contractName, contract);
        }

        //[APIInfo(typeof(PeerResult[]), "Returns list of known peers.", false, 30)]
        //public IAPIResult GetPeers()
        //{
        //    if (Node == null)
        //    {
        //        return new ErrorResult { error = "No node available" };
        //    }

        //    IEnumerable<Peer> allPeers = Node.Peers;

        //    if (Nexus.Name == DomainSettings.NexusMainnet)
        //    {
        //        // exclude fom the list all peers that did not configure external host properly
        //        allPeers = allPeers.Where(x => !x.Endpoint.Host.Contains("localhost"));
        //    }

        //    var peers = allPeers.Select(x => new PeerResult() { url = x.Endpoint.ToString(), version = x.Version, flags = x.Capabilities.ToString(), fee = x.MinimumFee.ToString(), pow = (uint)x.MinimumPoW }).ToList();

        //    peers.Add(new PeerResult() { url = $"{Node.PublicEndpoint}", version = Node.Version, flags = Node.Capabilities.ToString(), fee = Node.MinimumFee.ToString(), pow = (uint)Node.MinimumPoW });

        //    peers.Shuffle();

        //    return new ArrayResult()
        //    {
        //        values = peers.Select(x => (object)x).ToArray()
        //    };
        //}

        //[APIInfo(typeof(bool), "Writes a message to the relay network.", false, 0, true)]
        //public IAPIResult RelaySend([APIParameter("Serialized receipt, in hex", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string receiptHex)
        //{
        //    if (Node == null)
        //    {
        //        return new ErrorResult { error = "No node available" };
        //    }

        //    if (!Node.Capabilities.HasFlag(PeerCaps.Relay))
        //    {
        //        return new ErrorResult { error = "Node relay is disabled" };
        //    }

        //    byte[] bytes;
        //    RelayReceipt receipt;
        //    try
        //    {
        //        bytes = Base16.Decode(receiptHex);
        //        receipt = RelayReceipt.FromBytes(bytes);
        //    }
        //    catch
        //    {
        //        return new ErrorResult() { error = "error decoding receipt" };
        //    }

        //    var msgBytes = receipt.message.ToByteArray();
        //    if (!receipt.signature.Verify(msgBytes, receipt.message.sender))
        //    {
        //        return new ErrorResult() { error = "invalid signature" };
        //    }

        //    try
        //    {
        //        Node.PostRelayMessage(receipt);
        //    }
        //    catch (Exception e)
        //    {
        //        return new ErrorResult() { error = e.Message };
        //    }

        //    return new SingleResult()
        //    {
        //        value = true
        //    };
        //}

        //[APIInfo(typeof(ReceiptResult[]), "Receives messages from the relay network.", false)]
        //public IAPIResult RelayReceive([APIParameter("Address or account name", "helloman")] string account)
        //{
        //    if (Node == null)
        //    {
        //        return new ErrorResult { error = "No node available" };
        //    }

        //    if (!Node.Capabilities.HasFlag(PeerCaps.Relay))
        //    {
        //        return new ErrorResult { error = "Node relay is disabled" };
        //    }

        //    Address address;

        //    if (Address.IsValidAddress(account))
        //    {
        //        address = Address.FromText(account);
        //    }
        //    else
        //    {
        //        address = Nexus.LookUpName(Nexus.RootStorage, account);
        //        if (address.IsNull)
        //        {
        //            return new ErrorResult { error = "name not owned" };
        //        }
        //    }

        //    var receipts = Node.GetRelayReceipts(address);
        //    if (receipts.Any())
        //    {
        //        var receiptList = receipts.Select(x => (object)FillReceipt(x));

        //        return new ArrayResult() { values = receiptList.ToArray() };
        //    }
        //    else
        //    {
        //        return new ErrorResult { error = "no messages available" };
        //    }
        //}

        //[APIInfo(typeof(EventResult[]), "Reads pending messages from the relay network.", false)]
        //public IAPIResult GetEvents([APIParameter("Address or account name", "helloman")] string account)
        //{
        //    if (Node == null)
        //    {
        //        return new ErrorResult { error = "No node available" };
        //    }

        //    if (!Node.Capabilities.HasFlag(PeerCaps.Events))
        //    {
        //        return new ErrorResult { error = "Node relay is disabled" };
        //    }

        //    Address address;

        //    if (Address.IsValidAddress(account))
        //    {
        //        address = Address.FromText(account);
        //    }
        //    else
        //    {
        //        address = Nexus.LookUpName(Nexus.RootStorage, account);
        //        if (address.IsNull)
        //        {
        //            return new ErrorResult { error = "name not owned" };
        //        }
        //    }

        //    var events = Node.GetEvents(address);
        //    if (!events.Any())
        //    {
        //        return new ErrorResult { error = "not events available" };
        //    }

        //    var eventList = events.Select(x => (object)FillEvent(x));

        //    return new ArrayResult() { values = eventList.ToArray() };
        //}

        [APIInfo(typeof(PlatformResult[]), "Returns an array of available interop platforms.", false, 300)]
        public PlatformResult[] GetPlatforms()
        {
            var platformList = new List<PlatformResult>();

            RequireNexus();

            var platforms = Nexus.GetPlatforms(Nexus.RootStorage);
            var symbols = Nexus.GetTokens(Nexus.RootStorage);

            foreach (var platform in platforms)
            {
                var info = Nexus.GetPlatformInfo(Nexus.RootStorage, platform);
                var entry = new PlatformResult();
                entry.platform = platform;
                entry.interop = info.InteropAddresses.Select(x => new InteropResult()
                {
                    local = x.LocalAddress.Text,
                    external = x.ExternalAddress
                }).Reverse().ToArray();
                //TODO reverse array for now, only the last item is valid for now.
                entry.chain = DomainExtensions.GetChainAddress(info).Text;
                entry.fuel = info.Symbol;
                entry.tokens = symbols.Where(x => Nexus.HasTokenPlatformHash(x, platform, Nexus.RootStorage)).ToArray();
                platformList.Add(entry);
            }

            return platformList.ToArray();
        }

        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        public ValidatorResult[] GetValidators()
        {
            RequireNexus();

            var validators = Nexus.GetValidators().
                Where(x => !x.address.IsNull).
                Select(x => new ValidatorResult() { address = x.address.ToString(), type = x.type.ToString() });

            return validators.ToArray();
        }


        [APIInfo(typeof(string), "Tries to settle a pending swap for a specific hash.", false, 0, true)]
        public string SettleSwap([APIParameter("Name of platform where swap transaction was created", "phantasma")] string sourcePlatform
                , [APIParameter("Name of platform to settle", "phantasma")] string destPlatform
                , [APIParameter("Hash of transaction to settle", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            if (TokenSwapper == null)
            {
                throw new APIException("token swapper not available");
            }

            if (!TokenSwapper.SupportsSwap(sourcePlatform, destPlatform))
            {
                throw new APIException($"swaps between {sourcePlatform} and {destPlatform} not available");
            }

            RequireNexus();

            if (!Nexus.PlatformExists(Nexus.RootStorage, sourcePlatform))
            {
                throw new APIException("Invalid source platform");
            }

            if (!Nexus.PlatformExists(Nexus.RootStorage, destPlatform))
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
                    var swap = Nexus.RootChain.GetSwap(Nexus.RootStorage, hash);
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
                var destHash = TokenSwapper.SettleSwap(sourcePlatform, destPlatform, hash);

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
        public SwapResult[] GetSwapsForAddress([APIParameter("Address or account name", "helloman")] string account,
                string platform, bool extended = false)
        {
            if (TokenSwapper == null)
            {
                throw new APIException("token swapper not available");
            }

            RequireNexus();

            Address address;

            switch (platform)
            {
                case DomainSettings.PlatformName:
                    address = Address.FromText(account);
                    break;
                case Infrastructure.Chains.NeoWallet.NeoPlatform:
                    address = Infrastructure.Chains.NeoWallet.EncodeAddress(account);
                    break;
                case Infrastructure.Chains.EthereumWallet.EthereumPlatform:
                    address = Infrastructure.Chains.EthereumWallet.EncodeAddress(account);
                    break;
                case Pay.Chains.BSCWallet.BSCPlatform:
                    address = Pay.Chains.BSCWallet.EncodeAddress(account);
                    break;
                default:
                    address = Nexus.LookUpName(Nexus.RootStorage, account);
                    break;
            }

            if (address.IsNull)
            {
                throw new APIException("invalid address");
            }

            var swapList = TokenSwapper.GetPendingSwaps(address);

            var oracleReader = Nexus.GetOracleReader();

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
                var oldSwaps = (InteropHistory[])Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "interop", nameof(InteropContract.GetSwapsForAddress), address).ToObject();

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

        [APIInfo(typeof(string), "Returns latest sale hash.", false, -1)]
        public string GetLatestSaleHash()
        {
            RequireNexus();

            var hash = (Hash)Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "sale", nameof(SaleContract.GetLatestSaleHash)).ToObject();

            return hash.ToString();
        }

        [APIInfo(typeof(CrowdsaleResult), "Returns data about a crowdsale.", false, -1)]
        [APIFailCase("hash is invalid", "43242342")]
        public CrowdsaleResult GetSale([APIParameter("Hash of sale", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;
            if (!Hash.TryParse(hashText, out hash) || hash == Hash.Null)
            {
                throw new APIException("Invalid hash");
            }

            RequireNexus();

            var sale = (SaleInfo)Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "sale", nameof(SaleContract.GetSale), hash).ToObject();

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