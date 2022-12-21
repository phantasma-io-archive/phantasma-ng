using System;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

[Collection(nameof(SystemTestCollectionDefinition))]
public class GasContractTests
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

    public GasContractTests()
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
        reward = new StakeReward(user.Address, Timestamp.Now);
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
    public void TestGasContract()
    {
        var allowedGas = simulator.InvokeContract(NativeContractKind.Gas, nameof(GasContract.AllowedGas), user.Address).AsNumber();
        Assert.Equal(0, allowedGas);
        
        // Stake
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, initialAmount)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        var time = Timestamp.Now;
        
        var getLastInflationDate = simulator.InvokeContract(NativeContractKind.Gas, nameof(GasContract.GetLastInflationDate)).AsTimestamp();
        Assert.Equal(((DateTime)time).Year, ((DateTime)getLastInflationDate).Year);
        Assert.Equal(((DateTime)time).Month, ((DateTime)getLastInflationDate).Month);
        Assert.Equal(((DateTime)time).Day, ((DateTime)getLastInflationDate).Day);
        Assert.Equal(((DateTime)time).Hour, ((DateTime)getLastInflationDate).Hour);
        Assert.Equal(((DateTime)time).Hour, ((DateTime)getLastInflationDate).Hour);
        Assert.Equal(((DateTime)time).Minute, ((DateTime)getLastInflationDate).Minute);
        
        var getDaysUntilDistribution = simulator.InvokeContract(NativeContractKind.Gas, nameof(GasContract.GetDaysUntilDistribution)).AsNumber();
        Assert.Equal(40, getDaysUntilDistribution);
        
        // Time to the future
        simulator.TimeSkipDays(30);
        getDaysUntilDistribution = (uint)simulator.InvokeContract(NativeContractKind.Gas, nameof(GasContract.GetDaysUntilDistribution)).AsNumber();
        var myDistTime = new Timestamp((uint)getDaysUntilDistribution);
        Assert.Equal(0, 31-((DateTime)myDistTime).Day );
        
        BasicTransactionCall();
        getDaysUntilDistribution = (uint)simulator.InvokeContract(NativeContractKind.Gas, nameof(GasContract.GetDaysUntilDistribution)).AsNumber();
        myDistTime = new Timestamp((uint)getDaysUntilDistribution);
        Assert.Equal(0, 31-((DateTime)myDistTime).Day );
        
        simulator.TimeSkipDays(1);
        getDaysUntilDistribution = (uint)simulator.InvokeContract(NativeContractKind.Gas, nameof(GasContract.GetDaysUntilDistribution)).AsNumber();
        myDistTime = new Timestamp((uint)getDaysUntilDistribution);
        Assert.Equal(0, 1-((DateTime)myDistTime).Day );
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), user.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var myValue = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, "SOUL", user.Address);
        Assert.NotEqual(0, myValue);
    }

    private void BasicTransactionCall()
    {
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
    }
    
}
