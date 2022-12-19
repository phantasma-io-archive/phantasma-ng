using System;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Node.Oracles;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class NexusTestsSimulator
{
    
    Address sysAddress;
    PhantasmaKeys user;
    PhantasmaKeys owner;
    PhantasmaKeys owner2;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;
    StakeReward reward;

    public NexusTestsSimulator()
    {
        Initialize();
    }

    private void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Friends);
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        owner2 = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
        
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(new []{owner, owner2});
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        SetInitialBalance(user.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
    [Fact]
    public void TestGetTokenContract()
    {
        var soulAddress = SmartContract.GetAddressFromContractName("SOUL");
        var nullAddress = Address.Null;
        var contract = nexus.GetTokenContract(nexus.RootStorage, "SOUL");
        var contractByAddress = nexus.GetTokenContract(nexus.RootStorage, soulAddress);
        var nullContractByAddress = nexus.GetTokenContract(nexus.RootStorage, nullAddress);
        
        // Assert
        Assert.NotNull(contract);
        Assert.Equal(soulAddress, contract.Address);
        Assert.Equal("SOUL", contract.Name);
        Assert.Equal(contract.Name, contractByAddress.Name);
        Assert.Null(nullContractByAddress);
        
    }

    [Fact]
    public void TestHasTokenPlatform()
    {
        var exist = nexus.HasTokenPlatformHash("NEO", DomainSettings.PlatformName, nexus.RootStorage);
        var dontExist = nexus.HasTokenPlatformHash("NEO", "eth", nexus.RootStorage);
        
        // Assert
        Assert.True(exist);
        Assert.False(dontExist);
    }

    [Fact]
    public void TestGetPlatformTokenByHash()
    {
        var hash = new Hash(CryptoExtensions.Sha256("NEO"));
        var exist = nexus.GetPlatformTokenByHash(hash, DomainSettings.PlatformName, nexus.RootStorage);
        var existNEO = nexus.GetPlatformTokenByHash(hash, "neo", nexus.RootStorage);
        
        // Assert
        Assert.NotNull(exist);
        Assert.Equal("NEO", exist);
    }

    [Fact]
    public void TestGetPlatformTokenHashes()
    {
        var hash = new Hash(CryptoExtensions.Sha256("NEO"));
        var hashOnNeo = Hash.Parse("ED07CFFAD18F1308DB51920D99A2AF60AC66A7B3000000000000000000000000");
        var hashes = nexus.GetPlatformTokenHashes(DomainSettings.PlatformName, nexus.RootStorage);
        var hashesNeo = nexus.GetPlatformTokenHashes("neo", nexus.RootStorage);
        
        // Assert
        Assert.NotNull(hashes);
        Assert.Contains(hash, hashes);
        Assert.Contains(hashOnNeo, hashesNeo);
    }

    [Fact]
    public void TestGetTokenPlatformHash()
    {
        var hashNEO = new Hash(CryptoExtensions.Sha256("NEO"));
        var hashNEOonNeo = Hash.Parse("C56F33FC6ECFCD0C225C4AB356FEE59390AF8560BE0E930FAEBE74A6DAFF7C9B");
        var hash = nexus.GetTokenPlatformHash("NEO", DomainSettings.PlatformName, nexus.RootStorage);
        var hashNeoPlatform = nexus.GetTokenPlatformHash("NEO", "neo", nexus.RootStorage);
        
        // Assert
        Assert.NotNull(hash);
        Assert.Equal(hashNEO, hash);
        Assert.Equal(hashNEOonNeo, hashNeoPlatform);
    }

    [Fact]
    public void TestTokenExistsOnPlatform()
    {
        var existsPhantasma = nexus.TokenExistsOnPlatform("NEO", DomainSettings.PlatformName, nexus.RootStorage);
        var existsNeo = nexus.TokenExistsOnPlatform("NEO", "neo", nexus.RootStorage);
        var doesntExistNeo = nexus.TokenExistsOnPlatform("NEO", "eth", nexus.RootStorage);
        
        // Assert
        Assert.False(existsPhantasma);
        Assert.True(existsNeo);
        Assert.False(doesntExistNeo);
    }

    [Fact]
    public void TestGetFeeds()
    {
        var feeeds = nexus.GetFeeds(nexus.RootStorage);
        
        // Assert
        Assert.NotNull(feeeds);
    }

    [Fact]
    public void TestGetPlatforms()
    {
        var platforms = nexus.GetPlatforms(nexus.RootStorage);
        
        // Assert
        Assert.NotNull(platforms);
    }

    [Fact]
    public void TestIsPlatformAddress()
    {
        var address = PhantasmaKeys.Generate().Address;
        //Address.FromInterop(0x01, new byte[20]);
        var isAddressFalse = nexus.IsPlatformAddress(nexus.RootStorage, address);
        var isAddressFalseContract = nexus.IsPlatformAddress(nexus.RootStorage, sysAddress);
        //var isAddressTrue = nexus.IsPlatformAddress(nexus.RootStorage, sysAddress);
        
        // Assert
        Assert.False(isAddressFalse); 
        Assert.False(isAddressFalseContract);
    }

    [Fact (Skip = "Implement later")]
    public void TestRegisterPlatformAddress()
    {
        var localAddress = PhantasmaKeys.Generate().Address;
        var externalAddress = "0x18923u912u4912h9ashdf9asf";
        //nexus.RegisterPlatformAddress(nexus.RootStorage, "neo", localAddress, externalAddress);
    }

    [Fact]
    public void TestGetIndexOfChain()
    {
        var indexOfChain = nexus.GetIndexOfChain("main");
        var indexOfChainNone = nexus.GetIndexOfChain("main-example");
        
        // Assert
        Assert.Equal(0, indexOfChain);
        Assert.Equal(-1, indexOfChainNone);
    }

    [Fact]
    public void TestGetPlatformInfo()
    {
        Assert.Throws<ChainException>(() => nexus.GetPlatformInfo(nexus.RootStorage, "neo"));
    }

    [Fact]
    public void TestGetRelayBalance()
    {
        var address = PhantasmaKeys.Generate().Address;
        var relayBlanace = nexus.GetRelayBalance(address, Timestamp.Now);
        
        // Assert
        Assert.Equal(0, relayBlanace);
    }

    [Fact (Skip = "Implement later")]
    public void TestHasArchiveBlock()
    {
        //var hasArchive = nexus.HasArchiveBlock(, 0);
    }
    
    [Fact (Skip = "Implement later")]
    public void TestIstArchiveComplete()
    {
        //var hasArchive = nexus.HasArchiveBlock(, 0);
    }
    
    [Fact (Skip = "Implement later")]
    public void TestGetArchiveBlock()
    {
        //var block = nexus.GetArchiveBlock(0);
        
        // Assert
        //Assert.Null(block);
    }

    [Fact]
    public void TestGetValidatorByIndex()
    {
        var validator = nexus.GetValidatorByIndex(0, simulator.CurrentTime);
        
        // Assert
        Assert.NotNull(validator);
        Assert.Equal(owner.Address, validator.address);
    }

    [Fact]
    public void TestGetIndexOfValidator()
    {
        var index = nexus.GetIndexOfValidator(owner.Address, simulator.CurrentTime);
        
        // Assert
        Assert.NotNull(index);
        Assert.Equal(0, index);
    }

    [Fact]
    public void TestGetStakeTimestampOfAddress()
    {
        var time = nexus.GetStakeTimestampOfAddress(nexus.RootStorage, owner.Address, simulator.CurrentTime);
        
        // Assert
        Assert.NotNull(time);
        Assert.True((Timestamp)simulator.CurrentTime > time);
    }

    [Fact]
    public void TestGetUnclaimedFuelFromAddress()
    {
        var fuel = nexus.GetUnclaimedFuelFromAddress(nexus.RootStorage, owner.Address, simulator.CurrentTime);
        
        // Assert
        Assert.NotNull(fuel);
        Assert.True(fuel > 0);
    }

    [Fact]
    public void TestIsSecondaryValidator()
    {
        var secondary = nexus.IsSecondaryValidator(owner2.Address, simulator.CurrentTime);
        
        // Assert
        Assert.False(secondary);
    }
    
    [Fact]
    public void TestGetScondaryValidatorCount()
    {
        var count = nexus.GetSecondaryValidatorCount(simulator.CurrentTime);
        
        // Assert
        Assert.NotNull(count);
        Assert.Equal(2, count);
    }

    [Fact]
    public void TestGetPrimaryValidatorCount()
    {
        var count = nexus.GetPrimaryValidatorCount(simulator.CurrentTime);
        
        // Assert
        Assert.NotNull(count);
        Assert.Equal(2, count);
    }

    [Fact]
    public void TestGetValidators()
    {
        var validators = nexus.GetValidators(simulator.CurrentTime);
        
        // Assert
        Assert.NotNull(validators);
        Assert.Equal(2, validators.Length);
    }

    [Fact]
    public void TestGetValidatorLastActivity()
    {
        Assert.Throws<NotImplementedException>(() => nexus.GetValidatorLastActivity(owner.Address));
        /*var activity = nexus.GetValidatorLastActivity(owner.Address);
        
        // Assert
        Assert.NotNull(activity);
        Assert.Equal((Timestamp)simulator.CurrentTime, activity);*/
    }

    [Fact]
    public void TestHasNFT()
    {
        var nft = nexus.HasNFT(nexus.RootStorage, "CROWN", 0);
        
        // Assert
        Assert.False(nft);
    }

    [Fact]
    public void TestGetFeedInfo()
    {
        Assert.Throws<ChainException>(() => nexus.GetFeedInfo(nexus.RootStorage, "price://soul/usd"));
        
        // Assert
        
    }
    
    [Fact]
    public void TestCreateFeed()
    {
        var created = nexus.CreateFeed(nexus.RootStorage, owner.Address, "price://soul/usd", FeedMode.Last);
        
        var info = nexus.GetFeedInfo(nexus.RootStorage, "price://soul/usd");
        
        // Assert
        Assert.True(created);
        Assert.NotNull(info);
        Assert.Equal("price://soul/usd", info.Name);
        Assert.Equal(FeedMode.Last, info.Mode);
        
        // Change it
        created = nexus.CreateFeed(nexus.RootStorage, owner.Address, "price://soul/usd", FeedMode.Max);
        Assert.False(created);
        
        created = nexus.CreateFeed(nexus.RootStorage, owner.Address, null, FeedMode.Max);
        Assert.False(created);
    }

    [Fact]
    public void TestGetChildChainsByName()
    {
        var chain = nexus.GetChildChainsByName(nexus.RootStorage, "main");
        
        // Assert
        Assert.NotNull(chain);
    }
    
    [Fact]
    public void TestGetChildChainsByAddress()
    {
        var chain = nexus.GetChildChainsByAddress(nexus.RootStorage, owner.Address);
        
        // Assert
        Assert.Null(chain);
    }

    [Fact]
    public void TestGetChainOrganization()
    {
        var chain = nexus.GetChainOrganization("main");
        
        // Assert
        Assert.NotNull(chain);
    }

    [Fact]
    public void TestGetParentChainByAddress()
    {
        var chain = nexus.GetParentChainByAddress(owner.Address);
        
        // Assert
        Assert.Null(chain);
    }

    [Fact]
    public void TestFindTransactionByHash()
    {
        simulator.BeginBlock();
        var tx = simulator.GenerateTransfer(owner, user.Address, simulator.Nexus.RootChain, "SOUL", 10000000000);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        var transaction = nexus.FindTransactionByHash(tx.Hash);
        
        // Assert
        Assert.NotNull(transaction);
        Assert.Equal(tx.Payload, transaction.Payload);

    }
    
    [Fact]
    public void TestHasAddressScript()
    {
        var script = nexus.HasAddressScript(nexus.RootStorage, owner.Address, simulator.CurrentTime);
        
        // Assert
        Assert.False(script);
    }
    
    [Fact]
    public void TestLookUpAddressScript()
    {
        var script = nexus.LookUpAddressScript(nexus.RootStorage, owner.Address, simulator.CurrentTime);
        
        // Assert
        Assert.Equal(new byte[0], script);
    }

    [Fact]
    public void TestLoadNexus()
    {
        var _nexus = nexus.LoadNexus(nexus.RootStorage);
        
        // Assert
        Assert.NotNull(_nexus);
    }

    [Fact]
    public void TestAttach()
    {
        var oracle = new TestOracleObserver();
        nexus.Attach(oracle);
        nexus.Detach(oracle);
        
        // Assert
        Assert.NotNull(oracle);
    }

    
    
    private class TestOracleObserver : IOracleObserver
    {

        public void Update(INexus nexus, StorageContext storage)
        {
            // Do nothing
        }
    }

}