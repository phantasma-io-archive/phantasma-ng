using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Moq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Storage;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Core.Storage.Context;
using Phantasma.Infrastructure.RocksDB;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

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

        Nexus = new Nexus("unittest", 10000, name => new DBPartition(PartitionPath + name));
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

        var storage = (StorageContext)new KeyStoreStorage(Nexus.GetChainStorage("main"));
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

    [Fact]
    public void BeginInitialize_test_success()
    {
        var moq = CreateRuntimeMock();
        this.Nexus.BeginInitialize(moq.Object, User1.Address);

        var soulExists = this.Nexus.TokenExists(Context, DomainSettings.StakingTokenSymbol);
        soulExists.ShouldBeTrue();

        var kcalExists = this.Nexus.TokenExists(Context, DomainSettings.FuelTokenSymbol);
        kcalExists.ShouldBeTrue();
    }

    //[Fact]
    //public void BurnToken_test_success()
    //{
    //    this.Nexus.BurnToken();
    //}

    //[Fact]
    //public void BurnTokens_test_success()
    //{
    //    this.Nexus.BurnTokens();
    //}

    //[Fact]
    //public void ChainExists_test_success()
    //{
    //    this.Nexus.ChainExists();
    //}

    //[Fact]
    //public void ContractExists_test_success()
    //{
    //    this.Nexus.ContractExists();
    //}

    //[Fact]
    //public void CreateArchive_test_success()
    //{
    //    this.Nexus.CreateArchive();
    //}

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

    //[Fact]
    //public void CreateGenesisBlock_test_success()
    //{
    //    this.Nexus.CreateGenesisBlock();
    //}

    //[Fact]
    //public void CreateKeyStoreAdapter_test_success()
    //{
    //    this.Nexus.CreateKeyStoreAdapter();
    //}

    //[Fact]
    //public void CreateOrganization_test_success()
    //{
    //    this.Nexus.CreateOrganization();
    //}

    //[Fact]
    //public void CreatePlatform_test_success()
    //{
    //    this.Nexus.CreatePlatform();
    //}

    //[Fact]
    //public void CreateSeries_test_success()
    //{
    //    this.Nexus.CreateSeries();
    //}

    //[Fact]
    //public void CreateToken_test_success()
    //{
    //    this.Nexus.CreateToken();
    //}

    //[Fact]
    //public void DeleteArchive_test_success()
    //{
    //    this.Nexus.DeleteArchive();
    //}

    //[Fact]
    //public void DestroyNFT_test_success()
    //{
    //    this.Nexus.DestroyNFT();
    //}

    //[Fact]
    //public void Detach_test_success()
    //{
    //    this.Nexus.Detach();
    //}

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

    //[Fact]
    //public void FinishInitialize_test_success()
    //{
    //    this.Nexus.FinishInitialize();
    //}

    //[Fact]
    //public void GenerateNFT_test_success()
    //{
    //    this.Nexus.GenerateNFT();
    //}

    //[Fact]
    //public void GetAllSeriesForToken_test_success()
    //{
    //    this.Nexus.GetAllSeriesForToken();
    //}

    //[Fact]
    //public void GetArchive_test_success()
    //{
    //    this.Nexus.GetArchive();
    //}

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

    //[Fact]
    //public void GetChainByAddress_test_success()
    //{
    //    this.Nexus.GetChainByAddress();
    //}

    //[Fact]
    //public void GetChainByName_test_success()
    //{
    //    this.Nexus.GetChainByName();
    //}

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

        // setup Chain
        runtimeMoq.Setup(r => r.Chain).Returns(Chain);

        // setup allowance
        runtimeMoq.Setup(r => r.SubtractAllowance(It.IsAny<Address>(), It.IsAny<string>(), It.IsAny<BigInteger>()))
            .Returns(allowance);

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
