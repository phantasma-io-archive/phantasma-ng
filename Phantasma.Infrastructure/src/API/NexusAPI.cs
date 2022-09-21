using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Storage;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Shared;
using Phantasma.Shared.Utils;
using Tendermint.RPC;

namespace Phantasma.Infrastructure.API;

public static class NexusAPI
{
    public static Nexus Nexus { get; set; }
    public static ITokenSwapper TokenSwapper { get; set; }
    public static NodeRpcClient TRPC { get; set; }

    public static bool ApiLog { get; set; }

    public const int PaginationMaxResults = 99999;

    public static string ExternalHashToString(string platform, Hash hash, string symbol)
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
    public static void RequireNexus()
    {
        if (Nexus == null)
        {
            throw new Exception("Nexus not available locally");
        }
    }
    public static void RequireTokenSwapper()
    {
        if (TokenSwapper == null)
        {
            throw new Exception("Token swapper not available");
        }
    }

    public static Nexus GetNexus()
    {
        RequireNexus();

        return Nexus;
    }
    public static ITokenSwapper GetTokenSwapper()
    {
        RequireTokenSwapper();

        return TokenSwapper;
    }

    public static TokenResult FillToken(string tokenSymbol, bool fillSeries, bool extended)
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
            var platforms = Nexus.GetPlatforms(Nexus.RootStorage);
            foreach (var platform in platforms)
            {
                var extHash = Nexus.GetTokenPlatformHash(tokenSymbol, platform, Nexus.RootStorage);
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

    public static TokenDataResult FillNFT(string symbol, BigInteger ID, bool extended)
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
                        else if (symbol == DomainSettings.RewardTokenSymbol && method.name == "getInfoURL")
                        {
                            properties.Add(new TokenPropertyResult() { Key = "InfoURL", Value = "https://phantasma.io/crown/" + ID });
                        }
                        else if (symbol == DomainSettings.RewardTokenSymbol && method.name == "getName")
                        {
                            properties.Add(new TokenPropertyResult() { Key = "Name", Value = "Crown #" + info.MintID });
                        }
                        else
                        {
                            TokenUtils.FetchProperty(Nexus.RootStorage, chain, method.name, series, ID, (propName, propValue) =>
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

    public static AuctionResult FillAuction(MarketAuction auction, IChain chain)
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

    public static TransactionResult FillTransaction(Transaction tx)
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
            state = block != null ? block.GetStateForTransaction(tx.Hash).ToString() : ExecutionState.Break.ToString(),
            sender = tx.Sender.Text,
            gasPayer = tx.GasPayer.Text,
            gasTarget = tx.GasTarget.Text,
            gasPrice = tx.GasPrice.ToString(),
            gasLimit = tx.GasLimit.ToString(),
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

    public static EventResult FillEvent(Event evt)
    {
        return new EventResult
        {
            address = evt.Address.Text,
            contract = evt.Contract,
            data = evt.Data.Encode(),
            kind = evt.Kind >= EventKind.Custom ? ((byte)evt.Kind).ToString() : evt.Kind.ToString()
        };
    }

    public static OracleResult FillOracle(OracleEntry oracle)
    {
        return new OracleResult
        {
            url = oracle.URL,
            content = (oracle.Content.GetType() == typeof(byte[]))
                ? Base16.Encode(oracle.Content as byte[])
                : Base16.Encode(Serialization.Serialize(oracle.Content))
        };
    }

    public static BlockResult FillBlock(Block block, IChain chain)
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

    public static ChainResult FillChain(IChain chain)
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

    public static IChain FindChainByInput(string chainInput)
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

    public static ABIMethodResult[] FillMethods(IEnumerable<ContractMethod> methods)
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

    public static ContractResult FillContract(string name, SmartContract contract)
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

    public static ReceiptResult FillReceipt(RelayReceipt receipt)
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

    public static ArchiveResult FillArchive(IArchive archive)
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

    public static StorageResult FillStorage(Address address)
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

    public static AccountResult FillAccount(Address address)
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

        //result.relay = Nexus.GetRelayBalance(address).ToString();
        result.balances = balanceList.ToArray();
        result.validator = validator.ToString();

        result.txs = Nexus.RootChain.GetTransactionHashesForAddress(address).Select(x => x.ToString()).ToArray();

        return result;
    }

    public static JsonDocument ForwardTxAsync(string node, string tx)
    {
        var paramData = new List<object>();
        paramData.Add(tx);

        int retryCount = 0;
        do
        {
            var response = RequestUtils.RPCRequest(node, "broadcast_tx_async", out var _, 0, 1, paramData.ToArray());

            if (response != null)
            {
                if (response.RootElement.TryGetProperty("result", out var resultProperty))
                {
                    return response;
                }
            }

            retryCount++;
            Thread.Sleep(1000);

        } while (retryCount < 5);

        return null;
    }

    public static JsonDocument ForwardTxSync(string node, string tx)
    {
        var paramData = new List<object>();
        paramData.Add(tx);

        int retryCount = 0;
        do
        {
            var response = RequestUtils.RPCRequest(node, "broadcast_tx_sync", out var _, 0, 1, paramData.ToArray());

            if (response != null)
            {
                if (response.RootElement.TryGetProperty("result", out var resultProperty))
                {
                    return response;
                }
            }

            retryCount++;
            Thread.Sleep(1000);

        } while (retryCount < 5);

        return null;
    }
}
