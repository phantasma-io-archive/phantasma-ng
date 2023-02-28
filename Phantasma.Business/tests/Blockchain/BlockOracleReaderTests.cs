using System;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class BlockOracleReaderTests
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

    public BlockOracleReaderTests()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        reward = new StakeReward(user.Address, Timestamp.Now);
        InitializeSimulator();

        startBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
    }
        
    protected void InitializeSimulator()
    {
        simulator = new NexusSimulator(owner);
        nexus = simulator.Nexus;
        nexus.SetOracleReader(new OracleSimulator(nexus));
        simulator.GetFundsInTheFuture(owner);
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 9999)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), owner.Address)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
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
    public void TestReadOracle()
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        
        // Test reader
    }
    
    [Fact]
    public void TestSetCurrentHeight()
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        BlockOracleReader reader = new BlockOracleReader(nexus, block);

        var platformName = "ethereum";
        var chainName = "main";
        var height = "0";
        
        Assert.Throws<NotImplementedException>(() => reader.SetCurrentHeight(platformName, chainName, height ));
    }
    
    [Fact]
    public void TestGetCurrentHeight()
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        BlockOracleReader reader = new BlockOracleReader(nexus, block);

        var platformName = "ethereum";
        var chainName = "main";
        
        Assert.Throws<NotImplementedException>(() => reader.GetCurrentHeight(platformName, chainName ));
    }
    
    [Fact]
    public void TestReadAllBlocks()
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        BlockOracleReader reader = new BlockOracleReader(nexus, block);

        var platformName = "ethereum";
        var chainName = "main";
        
        Assert.Throws<NotImplementedException>(() => reader.ReadAllBlocks(platformName, chainName ));
    }

    [Fact]
    public void TestClear()
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        BlockOracleReader reader = new BlockOracleReader(nexus, block);

        Assert.Throws<NotImplementedException>(() => reader.Clear());
    }
    
    [Fact(Skip = "Not implemented")]
    public void TestRead()
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        
        var block = simulator.EndBlock().First();
        Assert.True(simulator.LastBlockWasSuccessful());

        var url = "price://KCAL";
        BlockOracleReader reader = new BlockOracleReader(nexus, block);

        Assert.Throws<NotImplementedException>(() =>
        {
            String x = ((OracleReader) reader).Read<String>(Timestamp.Now, url);
            return;
        } );
    }
}
