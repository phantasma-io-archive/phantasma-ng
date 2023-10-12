using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Archives;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Blockchain.Tokens.Structs;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Structs;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.Triggers;
using Phantasma.Core.Domain.Triggers.Enums;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Storage;
using Phantasma.Core.Types;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types.Structs;
using Phantasma.Infrastructure.RocksDB;
using Shouldly;
using Xunit;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class NexusTests : IDisposable
{
    public NexusTests()
    {
        TokenOwner = PhantasmaKeys.Generate();
        User1 = PhantasmaKeys.Generate();
        User2 = PhantasmaKeys.Generate();

        PartitionPath = Path.Combine(Path.GetTempPath(), "PhantasmaUnitTest", $"{Guid.NewGuid():N}") +
                        Path.DirectorySeparatorChar;
        Directory.CreateDirectory(PartitionPath);

        Nexus = Business.Blockchain.Nexus.Initialize<Chain>("unittest", name => new MemoryStore());
        var maxSupply = 10000000;

        var flags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable |
                    TokenFlags.Stakable | TokenFlags.Transferable;
        FungibleToken = new TokenInfo("SOUL", "PhantasmaStake", TokenOwner.Address, 0, 8, flags, new byte[1] { 0 },
            new ContractInterface());

        var nftFlags = TokenFlags.Burnable | TokenFlags.Mintable | TokenFlags.Stakable | TokenFlags.Transferable;
        NonFungibleToken = new TokenInfo("EXNFT", "Example NFT", TokenOwner.Address, 0, 0, nftFlags, new byte[1] { 0 },
            new ContractInterface());

        var ntFlags = TokenFlags.Burnable | TokenFlags.Divisible | TokenFlags.Fungible | TokenFlags.Mintable |
                      TokenFlags.Stakable;
        NonTransferableToken = new TokenInfo("EXNT", "Example Token non transferable", TokenOwner.Address, 0, 8,
            ntFlags, new byte[1] { 0 }, new ContractInterface());

        var storage = (StorageContext)new MemoryStorageContext();
        Context = new StorageChangeSetContext(storage);

        Chain = new Chain((Nexus)Nexus, "main");

        // setup balances
        var balances = new BalanceSheet(FungibleToken);
        balances.Add(Context, User1.Address, maxSupply);
    }

    private string PartitionPath { get; }
    private IToken FungibleToken { get; }
    private IToken NonFungibleToken { get; }
    private IToken NonTransferableToken { get; }
    private INexus Nexus { get; }
    private StorageChangeSetContext Context { get; }
    private PhantasmaKeys TokenOwner { get; }
    private PhantasmaKeys User1 { get; }
    private PhantasmaKeys User2 { get; }
    private Chain Chain { get; }
    private List<Event> Events { get; } = new();

    public void Dispose()
    {
        Events.Clear();
        Context.Clear();
    }

    [Fact]
    public void AddOwnerToArchive_test_success()
    {
        var merkleTree = new MerkleTree(new byte[] { 0, 0});
        var archive = new Archive(merkleTree, "testArchive", 100, Timestamp.Now, new SharedArchiveEncryption(),
            Enumerable.Range(0, (int)MerkleTree.GetChunkCountForSize(100)).ToList());
        this.Nexus.AddOwnerToArchive(Context, archive, User1.Address);

        var archiveNew = this.Nexus.GetArchive(Context, archive.Hash);

        archiveNew.IsOwner(User1.Address).ShouldBeTrue();
    }

    [Fact]
    public void ArchiveExists_test_success()
    {
        var merkleTree = new MerkleTree(new byte[] { 0, 0});
        var archive = new Archive(merkleTree, "testArchive", 100, Timestamp.Now, new SharedArchiveEncryption(),
            Enumerable.Range(0, (int)MerkleTree.GetChunkCountForSize(100)).ToList());
        this.Nexus.AddOwnerToArchive(Context, archive, User1.Address);

        var exists = this.Nexus.ArchiveExists(this.Context, archive.Hash);
        exists.ShouldBeTrue();
    }

    [Fact(Skip = "Ignore for now")]
    public void BeginInitialize_test_success()
    {
        var moq = CreateRuntimeMock();
        this.Nexus.BeginInitialize(moq.Object, User1.Address);

        var soulExists = this.Nexus.TokenExists(Context, DomainSettings.StakingTokenSymbol);
        soulExists.ShouldBeTrue();

        var kcalExists = this.Nexus.TokenExists(Context, DomainSettings.FuelTokenSymbol);
        kcalExists.ShouldBeTrue();
    }


    [Fact]
    public void BurnToken_test_success()
    {
        var moq = CreateRuntimeMock();
        var sheet = new SupplySheet(NonFungibleToken.Symbol, Nexus.RootChain, this.Nexus);
        sheet.Mint(Context, 1, 10);

        var content = new TokenContent(1, 1, "main", User1.Address, User2.Address, new byte[] {0}, new byte[] { 0 }, 1, null, TokenSeriesMode.Unique);
        var compressed = CompressionUtils.Compress(content.Serialize());

        Context.Put(Nexus.GetKeyForNFT(NonFungibleToken.Symbol, 1), compressed);

        var series = new TokenSeries(0, 100, TokenSeriesMode.Unique, new byte[0], new ContractInterface(), null);
        Context.Put(Nexus.GetTokenSeriesKey(NonFungibleToken.Symbol, content.SeriesID), series.Serialize());

        this.Nexus.BurnToken(moq.Object, NonFungibleToken, User1.Address, User2.Address, "main", 1);

        var sheetAfterBurn = new SupplySheet(NonFungibleToken.Symbol, Nexus.RootChain, this.Nexus);
        sheet.GetTotal(this.Context).ShouldBe(0);
    }

    //[Fact]
    //public void BurnTokens_test_success()
    //{
    //    this.Nexus.BurnTokens();
    //}

    [Fact]
    public void ChainExists_test_success()
    {
        Context.Put(".chain.name.main", this.Chain.Address.ToByteArray());
        var exists = this.Nexus.ChainExists(Context, "main");
        exists.ShouldBe(true);
    }

    [Fact]
    public void ChainExists_test_fail()
    {
        var exists = this.Nexus.ChainExists(Context, "");
        exists.ShouldBe(false);
    }

    [Fact]
    public void ContractExists_test_native_success()
    {
        var exists = this.Nexus.ContractExists(Context, "gas");
        exists.ShouldBe(true);
    }

    [Fact]
    public void ContractExists_test_success()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($".nexus.contract.other");
        Context.Put(bytes, new byte[0]);
        var exists = this.Nexus.ContractExists(Context, "other");
        exists.ShouldBe(true);
    }

    [Fact]
    public void CreateArchive_test_success()
    {
        var merkleTree = new MerkleTree(new byte[] { 0, 0});

        var archive = this.Nexus.CreateArchive(Context, merkleTree, User1.Address, "name", 100, Timestamp.Now, new SharedArchiveEncryption());

        archive.IsOwner(User1.Address).ShouldBe(true);
        archive.Size.ShouldBe(100);
        archive.Name.ShouldBe("name");
    }

    //[Fact]
    //public void CreateChain_test_success()
    //{
    //    this.Nexus.CreateChain();
    //}

    //[Fact]
    //public void CreateContract_test_success()
    //{
    //    this.Nexus.CreateContract();
    //}

    //[Fact]
    //public void CreateFeed_test_success()
    //{
    //    this.Nexus.CreateFeed();
    //}

    [Fact]
    public void CreateGenesisBlock_test_success()
    {
        this.Nexus.SetInitialValidators(new List<Address> { User1.Address, User2.Address });
        var tx = this.Nexus.CreateGenesisTransaction(Timestamp.Now, User1);
        tx.Script.Length.ShouldBeGreaterThan(0);
        tx.NexusName.ShouldBe("unittest");
        tx.ChainName.ShouldBe("main");
    }

    [Fact]
    public void CreateOrganization_test_success()
    {
        this.Nexus.CreateOrganization(Context, "someid", "somename", new byte[1] { 0 });
        var exists = this.Nexus.OrganizationExists(Context, "someid");
        exists.ShouldBe(true);
    }

    //[Fact]
    //public void CreatePlatform_test_success()
    //{
    //    this.Nexus.CreatePlatform();
    //}

    [Fact]
    public void CreateSeries_test_success()
    {
        var series = this.Nexus.CreateSeries(
                Context,
                NonFungibleToken,
                1,
                100,
                TokenSeriesMode.Unique,
                new byte[2] {0, 1},
                TokenUtils.GetNFTStandard()
                );
        series.Mode.ShouldBe(TokenSeriesMode.Unique);
        series.Script[0].ShouldBe((byte)0);
        series.Script[1].ShouldBe((byte)1);
        series.MaxSupply.ShouldBe(100);
    }

    [Fact]
    public void CreateToken_test_success()
    {
        var token = this.Nexus.CreateToken(
                Context,
                FungibleToken.Symbol,
                FungibleToken.Name,
                User1.Address,
                0,
                10,
                TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Stakable,
                new byte[2] { 0, 1 },
                new ContractInterface()
                );
        token.Name.ShouldBe(FungibleToken.Name);
        token.Decimals.ShouldBe(10);
        token.Symbol.ShouldBe(FungibleToken.Symbol);
    }

    [Fact]
    public void DeleteArchive_test_success()
    {
        var merkleTree = new MerkleTree(new byte[] { 0, 0});
        var archive = this.Nexus.CreateArchive(Context, merkleTree, User1.Address, "name", 100, Timestamp.Now, new SharedArchiveEncryption());

        // archive exists now
        var archiveNew = this.Nexus.GetArchive(Context, archive.Hash);
        archiveNew.Hash.ShouldBe(archive.Hash);

        this.Nexus.RemoveOwnerFromArchive(Context, archive, User1.Address);

        this.Nexus.DeleteArchive(Context, archive);

        // archive does not exist anymore
        var archiveNew2 = this.Nexus.GetArchive(Context, archive.Hash);
        archiveNew2.ShouldBeNull();
    }

    //[Fact]
    //public void FeedExists_test_success()
    //{
    //    this.Nexus.FeedExists();
    //}

    //[Fact]
    //public void FindBlockByTransaction_test_success()
    //{
    //    this.Nexus.FindBlockByTransaction();
    //}

    //[Fact]
    //public void FindBlockByTransactionHash_test_success()
    //{
    //    this.Nexus.FindBlockByTransactionHash();
    //}

    //[Fact]
    //public void FindTransactionByHash_test_success()
    //{
    //    this.Nexus.FindTransactionByHash();
    //}

    [Fact]
    public void FinishInitialize_test_success()
    {
        var runtimeMoq = CreateRuntimeMock();
        this.Nexus.FinishInitialize(runtimeMoq.Object, User1.Address);
    }

    [Fact]
    public void GenerateNFT_test_success()
    {
        var series = this.Nexus.CreateSeries(
                Context,
                NonFungibleToken,
                1,
                100,
                TokenSeriesMode.Unique,
                new byte[2] {0, 1},
                TokenUtils.GetNFTStandard()
                );

        var runtimeMoq = CreateRuntimeMock();
        var genID = this.Nexus.GenerateNFT(
                runtimeMoq.Object,
                NonFungibleToken.Symbol,
                "main",
                User1.Address,
                new byte[0],
                new byte[0],
                1);

        genID.ShouldBe(BigInteger.Parse("38772261170797515502142737251560910253885555854579348417967781179871348437219"));
    }

    [Fact]
    public void GetAllSeriesForToken_test_success()
    {
        var series = this.Nexus.CreateSeries(
                Context,
                NonFungibleToken,
                1,
                100,
                TokenSeriesMode.Unique,
                new byte[2] {0, 1},
                TokenUtils.GetNFTStandard()
                );

        var series2 = this.Nexus.CreateSeries(
                Context,
                NonFungibleToken,
                300,
                100,
                TokenSeriesMode.Unique,
                new byte[2] {0, 1},
                TokenUtils.GetNFTStandard()
                );

        var serieses = this.Nexus.GetAllSeriesForToken(Context, NonFungibleToken.Symbol);
        serieses.Length.ShouldBe(2);
        serieses.ShouldContain(300);
        serieses.ShouldContain(1);
    }

    [Fact]
    public void GetArchive_test_success()
    {
        var merkleTree = new MerkleTree(new byte[] { 0, 0});
        var archive = this.Nexus.CreateArchive(Context, merkleTree, User1.Address, "name", 100, Timestamp.Now, new SharedArchiveEncryption());

        // archive exists now
        var archiveNew = this.Nexus.GetArchive(Context, archive.Hash);
        archiveNew.Hash.ShouldBe(archive.Hash);
    }

    //[Fact]
    //public void GetBurnedTokenSupply_test_success()
    //{
    //    this.Nexus.GetBurnedTokenSupply();
    //}

    //[Fact]
    //public void GetBurnedTokenSupplyForSeries_test_success()
    //{
    //    this.Nexus.GetBurnedTokenSupplyForSeries();
    //}

    [Fact]
    public void GetChainByAddress_test_success()
    {
        var chain = this.Nexus.GetChainByAddress(this.Chain.Address);
        chain.Address.ShouldBe(this.Chain.Address);
    }

    [Fact]
    public void GetChainByName_test_success()
    {
        var chain = this.Nexus.GetChainByName("main");
        chain.Name.ShouldBe("main");
        chain.Address.ShouldBe(this.Chain.Address);
    }

    //[Fact]
    //public void GetChainOrganization_test_success()
    //{
    //    this.Nexus.GetChainOrganization();
    //}

    //[Fact]
    //public void GetChainStorage_test_success()
    //{
    //    this.Nexus.GetChainStorage();
    //}

    //[Fact]
    //public void GetChains_test_success()
    //{
    //    this.Nexus.GetChains();
    //}

    //[Fact]
    //public void GetChildChainsByAddress_test_success()
    //{
    //    this.Nexus.GetChildChainsByAddress();
    //}

    //[Fact]
    //public void GetChildChainsByName_test_success()
    //{
    //    this.Nexus.GetChildChainsByName();
    //}

    //[Fact]
    //public void GetContractByName_test_success()
    //{
    //    this.Nexus.GetContractByName();
    //}

    //[Fact]
    //public void GetContracts_test_success()
    //{
    //    this.Nexus.GetContracts();
    //}

    //[Fact]
    //public void GetFeedInfo_test_success()
    //{
    //    this.Nexus.GetFeedInfo();
    //}

    //[Fact]
    //public void GetFeeds_test_success()
    //{
    //    this.Nexus.GetFeeds();
    //}

    //[Fact]
    //public void GetGenesisAddress_test_success()
    //{
    //    this.Nexus.GetGenesisAddress();
    //}

    //[Fact]
    //public void GetGenesisBlock_test_success()
    //{
    //    this.Nexus.GetGenesisBlock();
    //}

    //[Fact]
    //public void GetGenesisHash_test_success()
    //{
    //    this.Nexus.GetGenesisHash();
    //}

    //[Fact]
    //public void GetGovernanceValue_test_success()
    //{
    //    this.Nexus.GetGovernanceValue();
    //}

    //[Fact]
    //public void GetIndexOfChain_test_success()
    //{
    //    this.Nexus.GetIndexOfChain();
    //}

    //[Fact]
    //public void GetIndexOfValidator_test_success()
    //{
    //    this.Nexus.GetIndexOfValidator();
    //}

    //[Fact]
    //public void GetKeyForNFT_test_success()
    //{
    //    this.Nexus.GetKeyForNFT();
    //}

    //[Fact]
    //public void GetNativeContractByAddress_test_success()
    //{
    //    this.Nexus.GetNativeContractByAddress();
    //}

    //[Fact]
    //public void GetNexusKey_test_success()
    //{
    //    this.Nexus.GetNexusKey();
    //}

    //[Fact]
    //public void GetOracleReader_test_success()
    //{
    //    this.Nexus.GetOracleReader();
    //}

    //[Fact]
    //public void GetOrganizationByAddress_test_success()
    //{
    //    this.Nexus.GetOrganizationByAddress();
    //}

    //[Fact]
    //public void GetOrganizationByName_test_success()
    //{
    //    this.Nexus.GetOrganizationByName();
    //}

    //[Fact]
    //public void GetOrganizations_test_success()
    //{
    //    this.Nexus.GetOrganizations();
    //}

    //[Fact]
    //public void GetParentChainByAddress_test_success()
    //{
    //    this.Nexus.GetParentChainByAddress();
    //}

    //[Fact]
    //public void GetParentChainByName_test_success()
    //{
    //    this.Nexus.GetParentChainByName();
    //}

    //[Fact]
    //public void GetPlatformInfo_test_success()
    //{
    //    this.Nexus.GetPlatformInfo();
    //}

    //[Fact]
    //public void GetPlatformTokenByHash_test_success()
    //{
    //    this.Nexus.GetPlatformTokenByHash();
    //}

    //[Fact]
    //public void GetPlatformTokenHashes_test_success()
    //{
    //    this.Nexus.GetPlatformTokenHashes();
    //}

    //[Fact]
    //public void GetPlatforms_test_success()
    //{
    //    this.Nexus.GetPlatforms();
    //}

    //[Fact]
    //public void GetPrimaryValidatorCount_test_success()
    //{
    //    this.Nexus.GetPrimaryValidatorCount();
    //}

    //[Fact]
    //public void GetProtocolVersion_test_success()
    //{
    //    this.Nexus.GetProtocolVersion();
    //}

    //[Fact]
    //public void GetRelayBalance_test_success()
    //{
    //    this.Nexus.GetRelayBalance();
    //}

    //[Fact]
    //public void GetSecondaryValidatorCount_test_success()
    //{
    //    this.Nexus.GetSecondaryValidatorCount();
    //}

    //[Fact]
    //public void GetStakeFromAddress_test_success()
    //{
    //    this.Nexus.GetStakeFromAddress();
    //}

    //[Fact]
    //public void GetStakeTimestampOfAddress_test_success()
    //{
    //    this.Nexus.GetStakeTimestampOfAddress();
    //}

    //[Fact]
    //public void GetTokenContract_test_success()
    //{
    //    this.Nexus.GetTokenContract();
    //}

    //[Fact]
    //public void GetTokenInfo_test_success()
    //{
    //    this.Nexus.GetTokenInfo();
    //}

    //[Fact]
    //public void GetTokenPlatformHash_test_success()
    //{
    //    this.Nexus.GetTokenPlatformHash();
    //}

    //[Fact]
    //public void GetTokenSeries_test_success()
    //{
    //    this.Nexus.GetTokenSeries();
    //}

    //[Fact]
    //public void GetTokens_test_success()
    //{
    //    this.Nexus.GetTokens();
    //}

    //[Fact]
    //public void GetUnclaimedFuelFromAddress_test_success()
    //{
    //    this.Nexus.GetUnclaimedFuelFromAddress();
    //}

    //[Fact]
    //public void GetValidator_test_success()
    //{
    //    this.Nexus.GetValidator();
    //}

    //[Fact]
    //public void GetValidatorByIndex_test_success()
    //{
    //    this.Nexus.GetValidatorByIndex();
    //}

    //[Fact]
    //public void GetValidatorLastActivity_test_success()
    //{
    //    this.Nexus.GetValidatorLastActivity();
    //}

    //[Fact]
    //public void GetValidatorType_test_success()
    //{
    //    this.Nexus.GetValidatorType();
    //}

    //[Fact]
    //public void GetValidators_test_success()
    //{
    //    this.Nexus.GetValidators();
    //}

    //[Fact]
    //public void HasAddressScript_test_success()
    //{
    //    this.Nexus.HasAddressScript();
    //}

    //[Fact]
    //public void HasArchiveBlock_test_success()
    //{
    //    this.Nexus.HasArchiveBlock();
    //}

    //[Fact]
    //public void HasNFT_test_success()
    //{
    //    this.Nexus.HasNFT();
    //}

    //[Fact]
    //public void HasTokenPlatformHash_test_success()
    //{
    //    this.Nexus.HasTokenPlatformHash();
    //}

    //[Fact]
    //public void InfuseToken_test_success()
    //{
    //    this.Nexus.InfuseToken();
    //}

    //[Fact]
    //public void IsArchiveComplete_test_success()
    //{
    //    this.Nexus.IsArchiveComplete();
    //}

    //[Fact]
    //public void IsKnownValidator_test_success()
    //{
    //    this.Nexus.IsKnownValidator();
    //}

    //[Fact]
    //public void IsNativeContract_test_success()
    //{
    //    this.Nexus.IsNativeContract();
    //}

    //[Fact]
    //public void IsNativeContractStatic_test_success()
    //{
    //    this.Nexus.IsNativeContractStatic();
    //}

    //[Fact]
    //public void IsPlatformAddress_test_success()
    //{
    //    this.Nexus.IsPlatformAddress();
    //}

    //[Fact]
    //public void IsPrimaryValidator_test_success()
    //{
    //    this.Nexus.IsPrimaryValidator();
    //}

    //[Fact]
    //public void IsSecondaryValidator_test_success()
    //{
    //    this.Nexus.IsSecondaryValidator();
    //}

    //[Fact]
    //public void IsStakeMaster_test_success()
    //{
    //    this.Nexus.IsStakeMaster();
    //}

    //[Fact]
    //public void LoadNexus_test_success()
    //{
    //    this.Nexus.LoadNexus();
    //}

    //[Fact]
    //public void LookUpAddressScript_test_success()
    //{
    //    this.Nexus.LookUpAddressScript();
    //}

    //[Fact]
    //public void LookUpChainNameByAddress_test_success()
    //{
    //    this.Nexus.LookUpChainNameByAddress();
    //}

    //[Fact]
    //public void LookUpName_test_success()
    //{
    //    this.Nexus.LookUpName();
    //}

    //[Fact]
    //public void MigrateTokenOwner_test_success()
    //{
    //    this.Nexus.MigrateTokenOwner();
    //}

    //[Fact]
    //public void MintToken_test_success()
    //{
    //    this.Nexus.MintToken();
    //}

    //[Fact]
    //public void MintTokens_test_success()
    //{
    //    this.Nexus.MintTokens();
    //}

    //[Fact]
    //public void Nexus_test_success()
    //{
    //    this.Nexus.Nexus();
    //}

    //[Fact]
    //public void Notify_test_success()
    //{
    //    this.Nexus.Notify();
    //}

    //[Fact]
    //public void OrganizationExists_test_success()
    //{
    //    this.Nexus.OrganizationExists();
    //}

    //[Fact]
    //public void PlatformExists_test_success()
    //{
    //    this.Nexus.PlatformExists();
    //}

    //[Fact]
    //public void ReadArchiveBlock_test_success()
    //{
    //    this.Nexus.ReadArchiveBlock();
    //}

    //[Fact]
    //public void ReadNFT_test_success()
    //{
    //    this.Nexus.ReadNFT();
    //}

    //[Fact]
    //public void RegisterPlatformAddress_test_success()
    //{
    //    this.Nexus.RegisterPlatformAddress();
    //}

    //[Fact]
    //public void RemoveOwnerFromArchive_test_success()
    //{
    //    this.Nexus.RemoveOwnerFromArchive();
    //}

    //[Fact]
    //public void SetOracleReader_test_success()
    //{
    //    this.Nexus.SetOracleReader();
    //}

    //[Fact]
    //public void SetPlatformTokenHash_test_success()
    //{
    //    this.Nexus.SetPlatformTokenHash();
    //}

    //[Fact]
    //public void TokenExists_test_success()
    //{
    //    this.Nexus.TokenExists();
    //}

    //[Fact]
    //public void TokenExistsOnPlatform_test_success()
    //{
    //    this.Nexus.TokenExistsOnPlatform();
    //}

    //[Fact]
    //public void TransferToken_test_success()
    //{
    //    this.Nexus.TransferToken();
    //}

    //[Fact]
    //public void TransferTokens_test_success()
    //{
    //    this.Nexus.TransferTokens();
    //}

    //[Fact]
    //public void UpgradeTokenContract_test_success()
    //{
    //    this.Nexus.UpgradeTokenContract();
    //}

    //[Fact]
    //public void WriteArchiveBlock_test_success()
    //{
    //    this.Nexus.WriteArchiveBlock();
    //}

    //[Fact]
    //public void WriteNFT_test_success()
    //{
    //    this.Nexus.WriteNFT();
    //}

    [Fact]
    public void simple_transfer_test_success()
    {
        var moq = CreateRuntimeMock();

        // transfer tokens
        Nexus.TransferTokens(moq.Object, FungibleToken, User1.Address, User2.Address, 10);

        // check balance
        var User2SoulBalance = new BalanceSheet(FungibleToken).Get(Context, User2.Address);
        User2SoulBalance.ShouldBe(10);

        // check events
        foreach (var evt in Events)
            switch (evt.Kind)
            {
                case EventKind.TokenReceive:
                case EventKind.TokenSend:
                    var data = evt.GetContent<TokenEventData>();
                    data.Symbol.ShouldBe(FungibleToken.Symbol);
                    data.Value.ShouldBe(10);
                    data.ChainName.ShouldBe("main");
                    break;
                default:
                    throw new NotImplementedException();
            }
    }

    private Mock<IRuntime> CreateRuntimeMock(bool isWitness = true, bool allowance = true)
    {
        var runtimeMoq = new Mock<IRuntime>();

        // setup Expect
        runtimeMoq.Setup(r => r.Expect(It.IsAny<bool>(), It.IsAny<string>()));

        // setup Storage
        runtimeMoq.Setup(r => r.Storage).Returns(Context);

        // setup RootStorage
        runtimeMoq.Setup(r => r.RootStorage).Returns(Context);

        // setup IsRootChain
        runtimeMoq.Setup(r => r.IsRootChain()).Returns(true);

        // setup Chain
        runtimeMoq.Setup(r => r.Chain).Returns(Chain);
        
       // runtimeMoq.Setup(r => r.WriteData(It.IsAny<Address>(), It.IsAny<byte[]>(), It.IsAny<byte[]>())).Verifiable();
        
        // setup GetToken
        runtimeMoq.Setup(r => r.GetToken(It.IsAny<string>())).Returns(NonFungibleToken);

        // setup allowance
        //runtimeMoq.Setup(r => r.SubtractAllowance(It.IsAny<Address>(), It.IsAny<string>(), It.IsAny<BigInteger>())).Returns(allowance);

        runtimeMoq.Setup(r =>
            r.CallContext(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<string>(),
                It.IsAny<object[]>())).Returns(VMObject.FromObject(1));


        // setup witness 
        runtimeMoq.Setup(r => r.IsWitness(It.IsAny<Address>())).Returns(isWitness);

        // setup Triggers
        runtimeMoq.SetupInvokeTriggerMoq(TriggerResult.Success, TriggerResult.Success);

        // setup Notify
        runtimeMoq.Setup(r => r.Notify(It.IsAny<EventKind>(), It.IsAny<Address>(), It.IsAny<byte[]>()))
            .Callback<EventKind, Address, byte[]>((evt, address, content) =>
            {
                Events.Add(new Event(evt, address, "", content));
            });

        return runtimeMoq;
    }
}
