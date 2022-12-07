using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

using Xunit;

using Phantasma.Business.Blockchain.Contracts;


[Collection(nameof(SystemTestCollectionDefinition))]
public class RelayContractTests
{
    PhantasmaKeys user;
    PhantasmaKeys user2;
    PhantasmaKeys user3;
    PhantasmaKeys owner;
    Nexus nexus;
    NexusSimulator simulator;
    int amountRequested;
    int gas;
    BigInteger initialAmount;
    BigInteger initialFuel;
    BigInteger startBalance;

    public RelayContractTests()
    {
        Initialize();
    }

    public void Initialize()
    {
        user = PhantasmaKeys.Generate();
        user2 = PhantasmaKeys.Generate();
        user3 = PhantasmaKeys.Generate();
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
        SetInitialBalance(user2.Address);
        SetInitialBalance(user3.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000000);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }

    [Fact]
    public void TestRelay()
    {
        
    }
}
