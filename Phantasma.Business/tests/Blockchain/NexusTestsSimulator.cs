using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

public class NexusTestsSimulator
{
    
    Address sysAddress;
    PhantasmaKeys user;
    PhantasmaKeys owner;
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
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
        
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(owner);
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
        var contract = nexus.GetTokenContract(nexus.RootStorage, "SOUL");
        
        // Assert
        Assert.NotNull(contract);
        Assert.Equal(soulAddress, contract.Address);
        Assert.Equal("SOUL", contract.Name);
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
    
    [Fact]
    public void TestGetArchiveBlock()
    {
        //var block = nexus.GetArchiveBlock(0);
        
        // Assert
        //Assert.Null(block);
    }
    
}
