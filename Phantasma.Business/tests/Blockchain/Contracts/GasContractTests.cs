using System;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Gas;
using Phantasma.Core.Domain.Contract.Gas.Structs;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts;

[Collection(nameof(SystemTestCollectionDefinition))]
public class GasContractTests
{
    PhantasmaKeys user;
    PhantasmaKeys user2;
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
        user = PhantasmaKeys.Generate();
        user2 = PhantasmaKeys.Generate();
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
        simulator = new NexusSimulator(new []{owner}, 19);
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

    [Fact]
    public void TestInflation()
    {
        // Phantom Force Organization
        var phantomOrg = simulator.Nexus.GetOrganizationByName(simulator.Nexus.RootStorage, DomainSettings.PhantomForceOrganizationName);
        var phantomForceBalanceBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, "SOUL", phantomOrg.Address);
        var phantomForceBalanceStackedBefore = simulator.Nexus.GetStakeFromAddress(simulator.Nexus.RootStorage,
            phantomOrg.Address, simulator.CurrentTime); 
        // Initial Supply - 171462300000000
        var tokenSupplySOUL = simulator.Nexus.RootChain.GetTokenSupply(simulator.Nexus.RootStorage, "SOUL");
        Assert.Equal(1171462300000000, tokenSupplySOUL);
        var InflationPerYear = 133;
        
        var currentSupply = tokenSupplySOUL;

        var minExpectedSupply = UnitConversion.ToBigInteger(100000000, DomainSettings.StakingTokenDecimals);
        if (currentSupply < minExpectedSupply)
        {
            currentSupply = minExpectedSupply;
        }

        var inflationBefore = currentSupply * 75 / 10000; 
        
        var inflationForEcosystem = inflationBefore * 33 / 100;
        var masterInflation = inflationForEcosystem* 20 / 100;
        var ecosystemLeftovers = inflationForEcosystem - masterInflation;
        var inflationForPhantomForce = inflationBefore * 33 / 100;
        var inflationForBP = inflationBefore * 33 / 100;
        var percentInflationLeftovers = inflationBefore * 1 / 100;
        
        var leftovers = inflationBefore - inflationForEcosystem - inflationForPhantomForce - inflationForBP - percentInflationLeftovers;
        
        // Skip time
        simulator.TimeSkipDays(90);
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        // Apply Inflation
        /*simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Gas, nameof(GasContract.ApplyInflation), owner.Address)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();*/
        simulator.TimeSkipHours(1);
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        var tokenSupplySOULAfter = simulator.Nexus.RootChain.GetTokenSupply(simulator.Nexus.RootStorage, "SOUL");
        Assert.NotEqual(tokenSupplySOUL, tokenSupplySOULAfter);
        
        var inflationAmountAfter = tokenSupplySOULAfter;
        Assert.Equal(inflationBefore + tokenSupplySOUL, inflationAmountAfter);
        
        
        // Check BP's 
        // Check Phantom Force DAO
        var phantomForceBalanceAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, "SOUL", phantomOrg.Address);
        var phantomForceBalanceStackedAfter = simulator.Nexus.GetStakeFromAddress(simulator.Nexus.RootStorage,
            phantomOrg.Address, simulator.CurrentTime); 
        Assert.Equal( phantomForceBalanceBefore + ecosystemLeftovers + leftovers + percentInflationLeftovers, phantomForceBalanceAfter);
        Assert.Equal( phantomForceBalanceStackedBefore + inflationForPhantomForce , phantomForceBalanceStackedAfter);
        
        
        simulator.TimeSkipDays(90);
        simulator.TimeSkipHours(1);
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        var totalSupplyAfterTwice = simulator.Nexus.RootChain.GetTokenSupply(simulator.Nexus.RootStorage, "SOUL");
        Assert.Equal(inflationBefore + tokenSupplySOULAfter, totalSupplyAfterTwice);
    }

    [Fact]
    public void TestSetEcosystemAndLeftoversAddress()
    {
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Gas, nameof(GasContract.SetEcosystemAddress), owner.Address, user.Address)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Gas, nameof(GasContract.SetLeftoversAddress), owner.Address, user2.Address)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        var user1BalancesBeforeInflation = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, "SOUL", user.Address);
        var user2BalancesBeforeInflation = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, "SOUL", user2.Address);
        
        // Skip time
        simulator.TimeSkipDays(90);
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        // Apply Inflation
        simulator.TimeSkipHours(1);
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);
        
        var user1BalancesAfterInflation = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, "SOUL", user.Address);
        var user2BalancesAfterInflation = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, "SOUL", user2.Address);
        
        Assert.NotEqual(user1BalancesBeforeInflation, user1BalancesAfterInflation);
        Assert.NotEqual(user2BalancesBeforeInflation, user2BalancesAfterInflation);
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
