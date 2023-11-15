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
using Phantasma.Infrastructure.API.Structs;
using Tendermint.RPC;
using Tendermint.RPC.Endpoint;
using Block = Phantasma.Core.Domain.Block;
using Phantasma.Core.Domain.TransactionData;
using TransactionResult = Phantasma.Infrastructure.API.Structs.TransactionResult;

namespace Phantasma.Infrastructure.API.Interfaces;

public interface IAPIService
{
    // Include method signatures that you've used in controllers.
    Nexus GetNexus();
    uint PaginationMaxResults { get; }
    NodeRpcClient TRPC { get; }
    List<ValidatorSettings> Validators { get; }
    IChain RootChain { get; }

    #region Account

    AccountResult FillAccount(Address address, bool extended);
    Address LookUpName(string name);
    string[] GetAddressesBySymbol(string symbol);

    #endregion

    #region Auction

    AuctionResult FillAuction(MarketAuction auctionID, IChain chain);

    #endregion

    #region Block

    BlockResult FillBlock(Block block, IChain chain);

    #endregion

    #region Chain

    IChain GetChainByName(string name);
    string[] GetChains();
    ChainResult FillChain(IChain chain, bool extended);
    IChain FindChainByInput(string chainAddressOrName);
    IChain GetChainByAddress(string chainInput);

    #endregion

    #region Connection

    ResultAbciQuery RequestBlock(int height);

    #endregion

    #region Contract

    ContractResult FillContract(string contractName, SmartContract contract, bool extended = true);

    #endregion

    #region Leaderboard

    LeaderboardResult GetLeaderboard(string name);

    #endregion

    #region Nexus

    NexusResult GetNexus(bool extended = false);

    #endregion

    #region Organization

    string[] GetOrganizations();
    bool OrganizationExists(string id);
    IOrganization GetOrganizationByName(string name, bool extended = true);

    #endregion

    #region Platform

    string[] GetPlatforms();
    PlatformInfo GetPlatformInfo(string platform);
    bool HasTokenPlatformHash(string symbol, string platform);

    #endregion

    #region RPC

    ResultAbciQuery AbciQuery(string path, string data = null, int height = 0, bool prove = false);
    ResultHealth Health();
    ResultStatus Status();
    ResultNetInfo NetInfo();

    #endregion

    #region Sale

    string GetLatestSaleHash();
    SaleInfo GetSale(Hash hash);

    #endregion

    #region Storage

    IArchive GetArchive(Hash hash);
    ArchiveResult FillArchive(IArchive archive);
    bool WriteArchiveBlock(IArchive archive, int blockIndex, byte[] bytes);
    byte[] ReadArchiveBlock(IArchive archive, int blockIndex);

    #endregion

    #region Swap

    string SettleSwap(string sourcePlatform, string destPlatform, string hashText);

    CrossChainTransfer[] GetSwapsForAddress([APIParameter("Address or account name", "helloman")] string account,
        string platform, bool extended = false);

    PlatformDetails GetPlatformDetails(string address, string platform);

    TokenSwapToSwap[] GetSwappersForPlatform(string platform);

    string SettleCrossChainSwap(string address, string sourcePlatform, string destPlatform, string hashText);

    #endregion

    #region Token

    bool TokenExists(string symbol);
    TokenContent ReadNft(string symbol, BigInteger id);
    string[] GetAvailableTokenSymbols();
    TokenResult FillToken(string symbol, bool fillSeries, bool extended);
    TokenDataResult FillNFT(string symbol, BigInteger ID, bool extended);
    IToken GetTokenInfo(string tokenSymbol);

    #endregion

    #region Transaction

    TransactionResult FillTransaction(Transaction tx);
    Transaction FindTransactionByHash(Hash hash);
    Block FindBlockByTransaction(Transaction tx);
    string SendRawTransaction(string txData);

    ScriptResult InvokeRawScript(string chainInput, string scriptData);

    #endregion

    #region Validator

    ValidatorEntry[] GetValidators();

    #endregion
}
