using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Tokens;

public class SupplySheetTest
{
    //GetChildBalance
    //MoveToParent
    //MoveFromParent
    //MoveToChild
    //MoveFromChild
    //GetTotal
    //Mint
    //Burn
    //Init
    //Synch
    
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
    
    public void TestSupplySheet()
    {
        //var x = new SupplySheet();
    }
    
    public void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Account);
        user = PhantasmaKeys.Generate();
        owner = PhantasmaKeys.Generate();
        amountRequested = 100000000;
        gas = 99999;
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
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
        simulator.LastBlockWasSuccessful().ShouldBeTrue();
    }
    
    /*[Fact]
    public void MoveToParent_WithValidAmount_ShouldUpdateBalances()
    {
        // Arrange
        var symbol = "TEST";
        var chain = nexus.RootChain;
        var storage = nexus.RootStorage;

        var supplySheet = new SupplySheet(symbol, chain, nexus);

        // Set initial balance
        supplySheet.Set(storage, chain.Name, 10);
        supplySheet.Set(storage, nexus.GetParentChainByName(chain.Name), 5);

        // Act
        var success = supplySheet.MoveToParent(storage, 5);

        // Assert
        Assert.True(success);

        // Check that local balance has decreased by the amount
        var localBalance = supplySheet.Get(storage, chain.Name);
        Assert.Equal(5, localBalance);

        // Check that parent balance has increased by the amount
        var parentBalance = supplySheet.Get(storage, nexus.GetParentChainByName(chain.Name));
        Assert.Equal(10, parentBalance);
    }
    
    [Fact]
    public void MoveFromParent_WithValidAmount_ShouldUpdateBalances()
    {
        // Arrange
        var symbol = "TEST";
        var chain = nexus.RootChain;
        var storage = nexus.RootStorage;

        var supplySheet = new SupplySheet(symbol, chain, nexus);

        // Set initial balance
        supplySheet.Set(storage, chain.Name, 10);
        supplySheet.Set(storage, nexus.GetParentChainByName(chain.Name), 5);

        // Act
        var success = supplySheet.MoveFromParent(storage, 5);

        // Assert
        Assert.True(success);

        // Check that local balance has increased by the amount
        var localBalance = supplySheet.Get(storage, chain.Name);
        Assert.Equal(15, localBalance);

        // Check that parent balance has decreased by the amount
        var parentBalance = supplySheet.Get(storage, nexus.GetParentChainByName(chain.Name));
        Assert.Equal(0, parentBalance);
    }*/
}
