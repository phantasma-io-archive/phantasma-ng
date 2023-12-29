using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Governance.Structs;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Contract.LeaderboardDetails.Structs;
using Phantasma.Core.Domain.Contract.Market.Structs;
using Phantasma.Core.Domain.Contract.Sale.Structs;
using Phantasma.Core.Domain.Contract.Validator.Structs;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Platform.Structs;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types.Structs;
using Phantasma.Infrastructure.API.Interfaces;
using Phantasma.Infrastructure.API.Structs;
using Phantasma.Infrastructure.Pay.Chains;
using Serilog;
using Tendermint.RPC;
using Tendermint.RPC.Endpoint;
using Phantasma.Core.Domain.TransactionData;
using Block = Phantasma.Core.Domain.Block;
using TransactionResult = Phantasma.Infrastructure.API.Structs.TransactionResult;

namespace Phantasma.Infrastructure.API;

public class APIChainService : IAPIService
{
    private IChain _rootChain;

    public uint PaginationMaxResults => NexusAPI.PaginationMaxResults;
    public NodeRpcClient TRPC => NexusAPI.TRPC;
    public List<ValidatorSettings> Validators => NexusAPI.Validators;

    IChain IAPIService.RootChain => _rootChain;

    /*public IChain RootChain()
    {
        return GetNexus().RootChain;
    }*/

    public Nexus GetNexus()
    {
        return NexusAPI.GetNexus();
    }

    #region Account

    public AccountResult FillAccount(Address address, bool extended)
    {
        return NexusAPI.FillAccount(address, extended);
    }

    public Address LookUpName(string name)
    {
        var nexus = GetNexus();
        return nexus.LookUpName(nexus.RootStorage, name, Timestamp.Now);
    }

    public string[] GetAddressesBySymbol(string symbol)
    {
        var nexus = GetNexus();
        return nexus.GetAddressesBySymbol(symbol);
    }

    #endregion

    #region Auction

    public AuctionResult FillAuction(MarketAuction auctionId, IChain chain)
    {
        return NexusAPI.FillAuction(auctionId, chain);
    }

    #endregion

    #region Block

    public BlockResult FillBlock(Block block, IChain chain)
    {
        return NexusAPI.FillBlock(block, chain);
    }

    #endregion

    #region Chain

    public IChain GetChainByName(string name)
    {
        var nexus = GetNexus();
        return nexus.GetChainByName(name);
    }

    public string[] GetChains()
    {
        var nexus = GetNexus();
        return nexus.GetChains(nexus.RootStorage);
    }

    public ChainResult FillChain(IChain chain, bool extended)
    {
        return NexusAPI.FillChain(chain, extended);
    }

    public IChain FindChainByInput(string chainAddressOrName)
    {
        return NexusAPI.FindChainByInput(chainAddressOrName);
    }

    public IChain GetChainByAddress(string chainInput)
    {
        var nexus = GetNexus();
        return nexus.GetChainByAddress(Address.FromText(chainInput));
    }

    #endregion

    #region Connection

    public ResultAbciQuery RequestBlock(int height)
    {
        var rpcClient = NexusAPI.TRPC;
        ResultAbciQuery result = new ResultAbciQuery();
        try
        {
            result = rpcClient.RequestBlock(height.ToString());
        }
        catch (Exception)
        {
            try
            {
                result.Response = new ResponseQuery();
                var chain = NexusAPI.Nexus.RootChain as Chain;
                var blockHash = chain.GetBlockHashAtHeight(height);
                var block = chain.GetBlockByHash(blockHash);
                var transactions = chain.GetBlockTransactions(block);
                var blockBytes = block.ToByteArray(true);
                var transactionsBytes = Serialization.Serialize(transactions.ToArray());

                var response = "block:" + Base16.Encode(blockBytes);
                response += "_transactions:" + Base16.Encode(transactionsBytes);

                result.Response.Code = 0;
                result.Response.Info = "Block get";
                result.Response.Value = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(response));
            }
            catch (Exception)
            {
                result.Response = new ResponseQuery();

                result.Response.Code = 1;
                result.Response.Info = "Block not found";
                result.Response.Value = null;
            }
        }

        return result;
    }

    #endregion

    #region Contract

    public ContractResult FillContract(string contractName, SmartContract contract, bool extended = true)
    {
        return NexusAPI.FillContract(contractName, contract, extended);
    }

    #endregion

    #region Leaderboard

    public LeaderboardResult GetLeaderboard(string name)
    {
        var nexus = NexusAPI.GetNexus();

        var temp = nexus.RootChain.InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, "ranking",
            nameof(RankingContract.GetRows), name);

        try
        {
            LeaderboardRow[] board = temp.ToArray<LeaderboardRow>();

            return new LeaderboardResult()
            {
                name = name,
                rows = board.Select(x => new LeaderboardRowResult()
                    { address = x.address.Text, value = x.score.ToString() }).ToArray(),
            };
        }
        catch (Exception e)
        {
            throw new APIException($"error fetching leaderboard: {e.Message}");
        }
    }

    #endregion

    #region Nexus

    public NexusResult GetNexus(bool extended)
    {
        var nexus = NexusAPI.GetNexus();

        var tokenList = new List<TokenResult>();

        var symbols = nexus.GetAvailableTokenSymbols(nexus.RootStorage);
        foreach (var token in symbols)
        {
            var entry = NexusAPI.FillToken(token, false, extended);
            tokenList.Add(entry);
        }

        var platformList = new List<PlatformResult>();

        var platforms = nexus.GetPlatforms(nexus.RootStorage);
        foreach (var platform in platforms)
        {
            var info = nexus.GetPlatformInfo(nexus.RootStorage, platform);

            var entry = new PlatformResult();
            entry.platform = platform;
            entry.interop = info.InteropAddresses.Select(x => new InteropResult()
            {
                local = x.LocalAddress.Text,
                external = x.ExternalAddress
            }).ToArray();
            entry.chain = DomainExtensions.GetChainAddress(info).Text;
            entry.fuel = info.Symbol;
            entry.tokens = symbols.Where(x => nexus.HasTokenPlatformHash(x, platform, nexus.RootStorage)).ToArray();
            platformList.Add(entry);
        }

        var chainList = new List<ChainResult>();

        var chains = nexus.GetChains(nexus.RootStorage);
        foreach (var chainName in chains)
        {
            var chain = nexus.GetChainByName(chainName);
            var single = NexusAPI.FillChain(chain, extended);
            chainList.Add(single);
        }

        var governance = (GovernancePair[])nexus.RootChain.InvokeContractAtTimestamp(nexus.RootChain.Storage,
            Timestamp.Now, "governance", nameof(GovernanceContract.GetValues)).ToArray<GovernancePair>();

        var orgs = nexus.GetOrganizations(nexus.RootStorage);

        return new NexusResult()
        {
            name = nexus.Name,
            protocol = nexus.GetProtocolVersion(nexus.RootStorage),
            tokens = tokenList.ToArray(),
            platforms = platformList.ToArray(),
            chains = chainList.ToArray(),
            organizations = orgs,
            governance = governance.Select(x => new GovernanceResult() { name = x.Name, value = x.Value.ToString() })
                .ToArray()
        };
    }

    #endregion

    #region Organization

    public string[] GetOrganizations()
    {
        var nexus = GetNexus();
        return nexus.GetOrganizations(nexus.RootStorage);
    }

    public bool OrganizationExists(string id)
    {
        var nexus = GetNexus();
        return nexus.OrganizationExists(nexus.RootStorage, id);
    }

    public IOrganization GetOrganizationByName(string name, bool extended = true)
    {
        var nexus = GetNexus();
        return nexus.GetOrganizationByName(nexus.RootStorage, name);
    }

    #endregion

    #region Platform

    public string[] GetPlatforms()
    {
        var nexus = GetNexus();
        return nexus.GetPlatforms(nexus.RootStorage);
    }

    public PlatformInfo GetPlatformInfo(string platform)
    {
        var nexus = GetNexus();

        return nexus.GetPlatformInfo(nexus.RootStorage, platform);
    }

    public bool HasTokenPlatformHash(string symbol, string platform)
    {
        var nexus = GetNexus();
        return nexus.HasTokenPlatformHash(symbol, platform, nexus.RootStorage);
    }

    #endregion

    #region RPC

    public ResultAbciQuery AbciQuery(string path, string data = null, int height = 0, bool prove = false)
    {
        return TRPC.AbciQuery(path, data, height, prove);
    }

    public ResultHealth Health()
    {
        return TRPC.Health();
    }

    public ResultStatus Status()
    {
        return TRPC.Status();
    }

    public ResultNetInfo NetInfo()
    {
        return TRPC.NetInfo();
    }

    #endregion

    #region Sale

    public string GetLatestSaleHash()
    {
        var nexus = NexusAPI.GetNexus();

        var hash = (Hash)nexus.RootChain.InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, "sale",
            nameof(SaleContract.GetLatestSaleHash)).ToObject();

        return hash.ToString();
    }

    public SaleInfo GetSale(Hash hash)
    {
        var nexus = NexusAPI.GetNexus();

        return (SaleInfo)nexus.RootChain.InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now,
            "sale", nameof(SaleContract.GetSale), hash).ToObject();
    }

    #endregion

    #region Storage

    public IArchive GetArchive(Hash hash)
    {
        var nexus = NexusAPI.GetNexus();

        return nexus.GetArchive(nexus.RootStorage, hash);
    }

    public ArchiveResult FillArchive(IArchive archive)
    {
        return NexusAPI.FillArchive(archive);
    }

    public bool WriteArchiveBlock(IArchive archive, int blockIndex, byte[] bytes)
    {
        var nexus = NexusAPI.GetNexus();

        try
        {
            nexus.WriteArchiveBlock(archive, blockIndex, bytes);
            return true;
        }
        catch (Exception e)
        {
            throw new APIException(e.Message);
        }
    }

    public byte[] ReadArchiveBlock(IArchive archive, int blockIndex)
    {
        var nexus = NexusAPI.GetNexus();

        try
        {
            var bytes = nexus.ReadArchiveBlock(archive, blockIndex);
            return bytes;
        }
        catch (Exception e)
        {
            throw new APIException(e.Message);
        }
    }

    #endregion

    #region Swap

    public string SettleSwap(string sourcePlatform, string destPlatform, string hashText)
    {
        var nexus = NexusAPI.GetNexus();
        var tokenSwapper = NexusAPI.GetTokenSwapper();

        // TODO: Change this call so it will trigger the checks on the chain
        // Get the information form the InteropContract All the Platforms
        // check if the sourcePlatform and destPlatform are valid

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
                Log.Information("Swap not found on Phantasma chain");
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

    public CrossChainTransfer[] GetSwapsForAddress([APIParameter("Address or account name", "helloman")] string account,
        string platform, bool extended = false)
    {
        var nexus = NexusAPI.GetNexus();

        Address address = platform switch
        {
            DomainSettings.PlatformName => Address.FromText(account),
            NeoWallet.NeoPlatform => NeoWallet.EncodeAddress(account),
            EthereumWallet.EthereumPlatform => EthereumWallet.EncodeAddress(account),
            Pay.Chains.BSCWallet.BSCPlatform => Pay.Chains.BSCWallet.EncodeAddress(account),
            _ => nexus.LookUpName(nexus.RootStorage, account, Timestamp.Now)
        };

        if (address.IsNull)
        {
            throw new APIException("invalid address");
        }

        var swaps = nexus.RootChain
            .InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, NativeContractKind.Interop,
                nameof(InteropContract.GetCrossChainTransfersForUser), address)
            .ToArray<CrossChainTransfer>();

        return swaps;
    }

    public PlatformDetails GetPlatformDetails(string address, string platform)
    {
        if (string.IsNullOrEmpty(address))
        {
            throw new APIException("Invalid address");
        }

        if (!Address.IsValidAddress(address))
        {
            throw new APIException("Invalid address");
        }

        var nexus = NexusAPI.GetNexus();
        var platformDetails = nexus.RootChain
            .InvokeContractAtTimestamp(nexus.RootChain.Storage, Timestamp.Now, NativeContractKind.Interop,
                nameof(InteropContract.GetPlatformDetailsForAddress), address, platform)
            .ToStruct<PlatformDetails>();
        return platformDetails;
    }

    public TokenSwapToSwap[] GetSwappersForPlatform(string platform)
    {
        var nexus = NexusAPI.GetNexus();
        return nexus.GetTokensSwapFromPlatform(platform, nexus.RootStorage);
    }

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
    #endregion

    #region Token

    public bool TokenExists(string symbol)
    {
        var nexus = GetNexus();
        return nexus.TokenExists(nexus.RootStorage, symbol);
    }

    public TokenContent ReadNft(string symbol, BigInteger id)
    {
        var nexus = GetNexus();
        return nexus.ReadNFT(nexus.RootStorage, symbol, id);
    }

    public string[] GetAvailableTokenSymbols()
    {
        var nexus = GetNexus();
        return nexus.GetAvailableTokenSymbols(nexus.RootStorage);
    }

    public TokenResult FillToken(string symbol, bool fillSeries, bool extended)
    {
        return NexusAPI.FillToken(symbol, fillSeries, extended);
    }

    public TokenDataResult FillNFT(string symbol, BigInteger ID, bool extended)
    {
        return NexusAPI.FillNFT(symbol, ID, extended);
    }

    public IToken GetTokenInfo(string tokenSymbol)
    {
        var nexus = GetNexus();
        return nexus.GetTokenInfo(nexus.RootStorage, tokenSymbol);
    }

    #endregion

    #region Transaction

    public TransactionResult FillTransaction(Transaction tx)
    {
        return NexusAPI.FillTransaction(tx);
    }

    public Transaction FindTransactionByHash(Hash hash)
    {
        var nexus = NexusAPI.GetNexus();
        return nexus.FindTransactionByHash(hash);
    }

    public Block FindBlockByTransaction(Transaction tx)
    {
        return GetNexus().FindBlockByTransaction(tx);
    }

    public string SendRawTransaction(string txData)
    {
        // TODO return error or tx result not just a string
        byte[] bytes;
        try
        {
            bytes = Base16.Decode(txData);
        }
        catch
        {
            return "Error while decoding the transaction.";
        }

        if (bytes.Length == 0)
        {
            return "Transaction length is equal to 0.";
        }

        // TODO store deserialized tx to save some time later on
        var tx = Transaction.Unserialize(bytes);
        if (tx == null)
        {
            return "Unserializing tx failed";
        }

        var res = NexusAPI.TRPC.BroadcastTxSync(txData);
        return res.Code != 0
            ? $"CheckTx returned code {res.Code} {res.Log}\nHash:{res.Hash}\nError:{res.Data}"
            : tx.Hash.ToString();
    }

    public ScriptResult InvokeRawScript(string chainInput, string scriptData)
    {
        var chain = NexusAPI.FindChainByInput(chainInput);
        try
        {
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

            var nexus = NexusAPI.GetNexus();

            var changeSet = new StorageChangeSetContext(chain.Storage);
            var oracle = nexus.GetOracleReader();
            uint offset = 0;

            // var resultInvokeScript = chain.InvokeScript(changeSet, script, Timestamp.Now);

            var vm = new RuntimeVM(-1, script, offset, chain, Address.Null, Timestamp.Now, Transaction.Null,
                changeSet, oracle, ChainTask.Null);

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

            var tempStack = vm.Stack.ToArray();

            var results = new Stack<string>();
            var singleResult = "";
            var resultReturn = new ScriptResult();
            if (vm != null)
            {
                if (vm.Stack != null)
                {
                    if (vm.Stack.Count == 0)
                    {
                        resultReturn.error = "\nStack is empty";
                    }

                    while (vm.Stack.Count > 0)
                    {
                        var result = vm.Stack.Pop();

                        if (result.Type == VMType.Object)
                        {
                            // NOTE currently supports simple arrays of C# objects. If something more complex in ncessary later, its good idea to rewrite this a recursive method
                            if (result.Data is Array)
                            {
                                var array1 = ((Array)result.Data);
                                var array2 = new VMObject[array1.Length];
                                for (int i = 0; i < array1.Length; i++)
                                {
                                    var obj = array1.GetValue(i);

                                    var vm_obj = VMObject.FromObject(obj);
                                    vm_obj = VMObject.CastTo(result, VMType.Struct);

                                    array2[i] = vm_obj;

                                    var resultBytesStruct = Serialization.Serialize(vm_obj);
                                    results.Push(Base16.Encode(resultBytesStruct));
                                }

                                result = VMObject.FromArray(array2);
                            }
                            else
                            {
                                result = VMObject.FromStruct(result.Data);
                            }
                        }
                        else if (result.Type == VMType.Struct)
                        {
                            if (result.GetArrayType() != VMType.None)
                            {
                                var array1 = (result.GetChildren());

                                var array2 = new VMObject[array1.Count];
                                for (int i = 0; i < array1.Count; i++)
                                {
                                    var obj = array1.ElementAt(i).Value;

                                    var vm_obj = VMObject.FromStruct(obj.Data);

                                    array2[i] = vm_obj;

                                    var resultBytesStruct = Serialization.Serialize(vm_obj);
                                    results.Push(Base16.Encode(resultBytesStruct));
                                }
                            }
                        }

                        var resultBytes = Serialization.Serialize(result);

                        if (result.GetArrayType() == VMType.None)
                        {
                            results.Push(Base16.Encode(resultBytes));
                        }

                        if (string.IsNullOrEmpty(singleResult))
                        {
                            singleResult = Base16.Encode(resultBytes);
                        }
                    }
                }
                else
                {
                    resultReturn.error = "\nStack is null";
                }
            }
            else
            {
                resultReturn.error = "\nVM is null";
            }

            var resultArray = results.ToArray();
            resultReturn.result = singleResult;
            resultReturn.results = resultArray;

            EventResult[] evts = new EventResult[0];

            if (vm != null)
            {
                if (vm.Events != null)
                {
                    evts = vm.Events.Select(evt => new EventResult()
                        {
                            address = evt.Address.Text, kind = evt.Kind.ToString(), data = Base16.Encode(evt.Data)
                        })
                        .ToArray();
                    resultReturn.events = evts;
                }
                else
                {
                    resultReturn.error += "\nEvents is null";
                }
            }
            else
            {
                resultReturn.error += "\nVM is null";
            }

            OracleResult[] oracleReads = new OracleResult[0];
            if (oracle != null)
            {
                if (oracle.Entries != null)
                {
                    oracleReads = oracle.Entries.Select(x => new OracleResult()
                    {
                        url = x.URL,
                        content = Base16.Encode((x.Content.GetType() == typeof(byte[])
                            ? x.Content as byte[]
                            : Serialization.Serialize(x.Content)))
                    }).ToArray();
                    resultReturn.oracles = oracleReads;
                }
                else
                {
                    resultReturn.error += "\nOracle Entries is null";
                }
            }
            else
            {
                resultReturn.error += "\nOracle is null";
            }


            return resultReturn;
        }
        catch (APIException apiException)
        {
            Log.Error($"API - Call error -> {apiException.Message}");
            throw;
        }
        catch (Exception e)
        {
            var result = new ScriptResult();
            result.error = e.Message;
            return result;
        }
    }

    #endregion

    #region Validator

    public ValidatorEntry[] GetValidators()
    {
        var nexus = NexusAPI.GetNexus();
        return nexus.GetValidators(Timestamp.Now);
    }

    #endregion
}
