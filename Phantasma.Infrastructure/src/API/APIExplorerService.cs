using System;
using System.Collections.Generic;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Contract.Market.Structs;
using Phantasma.Core.Domain.Contract.Sale.Structs;
using Phantasma.Core.Domain.Contract.Validator.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Platform.Structs;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Infrastructure.API.Interfaces;
using Phantasma.Infrastructure.API.Structs;
using Tendermint.RPC;
using Tendermint.RPC.Endpoint;
using Block = Phantasma.Core.Domain.Block;
using TransactionResult = Phantasma.Infrastructure.API.Structs.TransactionResult;

namespace Phantasma.Infrastructure.API;

public class APIExplorerService : IAPIService
{
    public Nexus GetNexus()
    {
        throw new NotImplementedException();
    }

    public uint PaginationMaxResults { get; }
    public NodeRpcClient TRPC { get; }
    public List<ValidatorSettings> Validators { get; }
    public IChain RootChain { get; }

    public AccountResult FillAccount(Address address, bool extended)
    {
        return new AccountResult();
    }

    public Address LookUpName(string name)
    {
        throw new NotImplementedException();
    }

    public string[] GetAddressesBySymbol(string symbol)
    {
        throw new NotImplementedException();
    }

    public AuctionResult FillAuction(MarketAuction auctionID, IChain chain)
    {
        throw new NotImplementedException();
    }

    public BlockResult FillBlock(Block block, IChain chain)
    {
        throw new NotImplementedException();
    }

    public IChain GetChainByName(string name)
    {
        throw new NotImplementedException();
    }

    public string[] GetChains()
    {
        throw new NotImplementedException();
    }

    public ChainResult FillChain(IChain chain, bool extended)
    {
        throw new NotImplementedException();
    }

    public IChain FindChainByInput(string chainAddressOrName)
    {
        throw new NotImplementedException();
    }

    public IChain GetChainByAddress(string chainInput)
    {
        throw new NotImplementedException();
    }

    public ResultAbciQuery RequestBlock(int height)
    {
        throw new NotImplementedException();
    }

    public ContractResult FillContract(string contractName, SmartContract contract, bool extended = true)
    {
        throw new NotImplementedException();
    }

    public LeaderboardResult GetLeaderboard(string name)
    {
        throw new NotImplementedException();
    }

    public NexusResult GetNexus(bool extended = false)
    {
        throw new NotImplementedException();
    }

    public string[] GetOrganizations()
    {
        throw new NotImplementedException();
    }

    public bool OrganizationExists(string id)
    {
        throw new NotImplementedException();
    }

    public IOrganization GetOrganizationByName(string name, bool extended = true)
    {
        throw new NotImplementedException();
    }

    public string[] GetPlatforms()
    {
        throw new NotImplementedException();
    }

    public PlatformInfo GetPlatformInfo(string platform)
    {
        throw new NotImplementedException();
    }

    public bool HasTokenPlatformHash(string symbol, string platform)
    {
        throw new NotImplementedException();
    }

    public ResultAbciQuery AbciQuery(string path, string data = null, int height = 0, bool prove = false)
    {
        throw new NotImplementedException();
    }

    public ResultHealth Health()
    {
        throw new NotImplementedException();
    }

    public ResultStatus Status()
    {
        throw new NotImplementedException();
    }

    public ResultNetInfo NetInfo()
    {
        throw new NotImplementedException();
    }

    public string GetLatestSaleHash()
    {
        throw new NotImplementedException();
    }

    public SaleInfo GetSale(Hash hash)
    {
        throw new NotImplementedException();
    }

    public IArchive GetArchive(Hash hash)
    {
        throw new NotImplementedException();
    }

    public ArchiveResult FillArchive(IArchive archive)
    {
        throw new NotImplementedException();
    }

    public bool WriteArchiveBlock(IArchive archive, int blockIndex, byte[] bytes)
    {
        throw new NotImplementedException();
    }

    public byte[] ReadArchiveBlock(IArchive archive, int blockIndex)
    {
        throw new NotImplementedException();
    }

    public string SettleSwap(string sourcePlatform, string destPlatform, string hashText)
    {
        throw new NotImplementedException();
    }

    CrossChainTransfer[] IAPIService.GetSwapsForAddress(string account, string platform, bool extended)
    {
        throw new NotImplementedException();
    }

    public PlatformDetails GetPlatformDetails(string address, string platform)
    {
        throw new NotImplementedException();
    }

    public TokenSwapToSwap[] GetSwappersForPlatform(string platform)
    {
        throw new NotImplementedException();
    }

    public string SettleCrossChainSwap(string address, string sourcePlatform, string destPlatform, string hashText)
    {
        throw new NotImplementedException();
    }

    public SwapResult[] GetSwapsForAddress(string account, string platform, bool extended = false)
    {
        throw new NotImplementedException();
    }

    public bool TokenExists(string symbol)
    {
        throw new NotImplementedException();
    }

    public TokenContent ReadNft(string symbol, BigInteger id)
    {
        throw new NotImplementedException();
    }

    public string[] GetAvailableTokenSymbols()
    {
        throw new NotImplementedException();
    }

    public TokenResult FillToken(string symbol, bool fillSeries, bool extended)
    {
        throw new NotImplementedException();
    }

    public TokenPriceResult[] FillTokenPrice(string symbol)
    {
        throw new NotImplementedException();
    }

    public TokenDataResult FillNFT(string symbol, BigInteger ID, bool extended)
    {
        throw new NotImplementedException();
    }

    public IToken GetTokenInfo(string tokenSymbol)
    {
        throw new NotImplementedException();
    }

    public TransactionResult FillTransaction(Transaction tx)
    {
        throw new NotImplementedException();
    }

    Transaction IAPIService.FindTransactionByHash(Hash hash)
    {
        return FindTransactionByHash(hash);
    }

    Block IAPIService.FindBlockByTransaction(Transaction tx)
    {
        return FindBlockByTransaction(tx);
    }

    public Transaction FindTransactionByHash(Hash hash)
    {
        throw new NotImplementedException();
    }

    public Block FindBlockByTransaction(Transaction tx)
    {
        throw new NotImplementedException();
    }

    public string SendRawTransaction(string txData)
    {
        throw new NotImplementedException();
    }

    public ScriptResult InvokeRawScript(string chainInput, string scriptData)
    {
        throw new NotImplementedException();
    }

    public ValidatorEntry[] GetValidators()
    {
        throw new NotImplementedException();
    }
}
