using System;
using System.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.Pay.Chains;

using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection(nameof(SystemTestCollectionDefinition))]
public class SwapContractTest
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

    public SwapContractTest()
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
    public void TestSwaping()
    {
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.StakingTokenSymbol, 100000000)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.FuelTokenSymbol, initialFuel)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, user.Address);
        var startingKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, user.Address);

        BigInteger swapAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals) / 100;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapTokens), user.Address, DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, swapAmount)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var currentSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, user.Address);
        var currentKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, user.Address);

        Assert.True(currentSoulBalance < startingSoulBalance, $"{currentSoulBalance} < {startingSoulBalance}");
        Assert.True(currentKcalBalance > startingKcalBalance);
    }
    
    [Fact]
    public void CosmicSwap()
    {
        var testUser = PhantasmaKeys.Generate();

        var soulAmount = UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals);
        var soulUserAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        var kcalAmount = UnitConversion.ToBigInteger(1000, DomainSettings.FuelTokenDecimals);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.StakingTokenSymbol, soulAmount)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.FuelTokenSymbol, kcalAmount)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, soulUserAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var startingKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        BigInteger swapAmount = UnitConversion.ToBigInteger(1m, DomainSettings.StakingTokenDecimals);
        
        // Should Pass
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), testUser.Address, DomainSettings.StakingTokenSymbol, swapAmount)
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 999)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);

        var currentSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var currentKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.True(currentSoulBalance < startingSoulBalance, $"{currentSoulBalance} < {startingSoulBalance}");
        Assert.True(currentKcalBalance > startingKcalBalance);
    }

    [Fact]
    public void CosmicSwapFail()
    {
        var testUser = PhantasmaKeys.Generate();

        var soulAmount = UnitConversion.ToBigInteger(1000, 8);
        var soulUserAmount = UnitConversion.ToBigInteger(10, 8);
        var kcalAmount = UnitConversion.ToBigInteger(1000, 10);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.StakingTokenSymbol, soulAmount)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.FuelTokenSymbol, kcalAmount)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, soulUserAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var startingKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        BigInteger swapAmount = UnitConversion.ToBigInteger(2, 8);
        
        // Should fail -> Order of scripts
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), testUser.Address, DomainSettings.StakingTokenSymbol, swapAmount)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
        
        var currentSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var currentKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.False(currentSoulBalance < startingSoulBalance, $"{currentSoulBalance} < {startingSoulBalance}");
        Assert.False(currentKcalBalance > startingKcalBalance, $"{currentKcalBalance} > {startingKcalBalance}");
    }

    [Fact]
    public void TestCosmicSwapsWithoutFunds()
    {
        var testUser = PhantasmaKeys.Generate();

        var soulAmount = UnitConversion.ToBigInteger(1000, 8);
        var soulUserAmount = UnitConversion.ToBigInteger(10, 8);
        var kcalAmount = UnitConversion.ToBigInteger(1000, 10);

        simulator.BeginBlock();
        
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
            soulUserAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var startingSoulBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var startingKcalBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        BigInteger swapAmount = UnitConversion.ToBigInteger(0.5m, 8);

        // Should fail -> Order of scripts
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), testUser.Address,
                    DomainSettings.StakingTokenSymbol, swapAmount)
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .SpendGas(testUser.Address)
                .EndScript()); 
        
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful()); // this regarding the first transaction not this one.
        

        var currentSoulBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var currentKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.False(currentSoulBalance < startingSoulBalance, $"{currentSoulBalance} < {startingSoulBalance}");
        Assert.False(currentKcalBalance > startingKcalBalance, $"{currentKcalBalance} > {startingKcalBalance}");
    }

    [Fact]
    public void NoTokensInSwap()
    {
        var amount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        var testUser = PhantasmaKeys.Generate();
        var feeAmount = UnitConversion.ToBigInteger(0.5m, DomainSettings.FuelTokenDecimals);

        
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, amount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Add Random transaction just to accumulate fees
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, SmartContract.GetAddressForNative(NativeContractKind.Gas), nexus.RootChain, DomainSettings.FuelTokenSymbol, feeAmount*2);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        

        // Swap Fee
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), testUser.Address, DomainSettings.StakingTokenSymbol, feeAmount)
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .SpendGas(testUser.Address)
                .EndScript());
        // This is because the pot is empty
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());
    }

    /*
        [Fact]
        public void GetRatesForSwap()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var script = new ScriptBuilder().CallContract("swap", "GetRates", "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)).EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            var temp = result.ToObject();
            var rates = (SwapPair[])temp;

            decimal targetRate = 0;

            foreach (var entry in rates)
            {
                if (entry.Symbol == DomainSettings.FuelTokenSymbol)
                {
                    targetRate = UnitConversion.ToDecimal(entry.Value, DomainSettings.FuelTokenDecimals);
                    break;
                }
            }

            Assert.True(targetRate == 5m);
        }*/

}
