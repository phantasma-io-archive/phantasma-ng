using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Stake;
using Phantasma.Core.Domain.Contract.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Tasks;
using Phantasma.Core.Domain.Tasks.Enum;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Phantasma.Core.Numerics;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class ChainTestsSimulator
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
    IChain chain;

    public ChainTestsSimulator()
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
        chain = nexus.RootChain;
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
    public void TestChainToString()
    {
        var toString = chain.ToString();
        
        Assert.NotNull(toString);
    }

    [Fact]
    public void TestContainsBlockHash()
    {
        var hash = new Hash(CryptoExtensions.Sha256("test"));
        var contains = chain.ContainsBlockHash(hash);
        
        Assert.False(contains);
    }

    [Fact]
    public void TestGetLastActivityOfAddress()
    {
        var lastActivity = chain.GetLastActivityOfAddress(owner.Address);
        
        Assert.NotNull(lastActivity);
    }

    [Fact]
    public void TestGetTransactionCount()
    {
        var count = chain.GetTransactionCount();
        
        Assert.Equal(3, count);
    }

    [Fact]
    public void TestGetSwap()
    {
        var hash = new Hash(CryptoExtensions.Sha256("test"));
        Assert.Throws<ChainException>(()=>chain.GetSwap(nexus.RootStorage, hash));
    }

    [Fact]
    public void TestGetSwapHashesForAddress()
    {
        var hashes = chain.GetSwapHashesForAddress(nexus.RootStorage, owner.Address);
        
        Assert.NotNull(hashes);
    }

    [Fact]
    public void TestTask()
    {
        ContractParameter param = new ContractParameter();
        ContractMethod method = new ContractMethod("test", VMType.Number, 0, param);
        var task = chain.StartTask(nexus.RootStorage, owner.Address, "account", method, 10, 5, TaskFrequencyMode.Blocks, 10000);
        var getTask = chain.GetTask(nexus.RootStorage, task.ID);
        chain.StopTask(nexus.RootStorage, task.ID);
        
        
        Assert.NotNull(task);
        Assert.NotNull(getTask);
        Assert.Equal(owner.Address, task.Owner);
        Assert.Equal(owner.Address, getTask.Owner);
    }
}
