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

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection(nameof(SystemTestCollectionDefinition))]
public class StakeContractTestLegacy
{
    public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);
    public static BigInteger MinimumGasLimit = 99999;
    public static BigInteger BaseSOULBalance = UnitConversion.ToBigInteger(1000, 8);
    public static BigInteger BaseKCALBalance = UnitConversion.ToBigInteger(100, 10);

    public uint DefaultEnergyRatioDivisor => StakeContract.DefaultEnergyRatioDivisor;

    public BigInteger StakeToFuel(BigInteger stakeAmount, uint _currentEnergyRatioDivisor)
    {
        return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _currentEnergyRatioDivisor;
    }
    
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

    public StakeContractTestLegacy()
    {
        Initialize();
    }
    
    public void Initialize()
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
        simulator.GetFundsInTheFuture(owner, 5);
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);

        for (int i = 0; i < 5; i++)
        {
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), owner.Address)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.EndBlock();
        }
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
    public void TestEnergyRatioDecimals()
    {
        var testUser = PhantasmaKeys.Generate();
        var stakeAmount = MinimumValidStake;
        double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
        double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeAmount)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

        Assert.True(realUnclaimedAmount == realExpectedUnclaimedAmount);

        BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
        Assert.True(actualEnergyRatio == DefaultEnergyRatioDivisor);
    }
    
    [Fact]
    public void TestGetUnclaimed()
    {
        var testUser = PhantasmaKeys.Generate();
        var stakeAmount = MinimumValidStake;
        var expectedUnclaimedAmount = StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor);

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount*50);
        simulator.EndBlock();

        //var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime + TimeSpan.FromMinutes(2), NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeAmount).
                SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var unclaimedValue = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake,
            nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();

        Assert.True(unclaimedValue == expectedUnclaimedAmount, $"{unclaimedValue} == {expectedUnclaimedAmount}");
    }
    
    [Fact]
    public void TestUnstake()
    {
        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        //-----------
        //Perform a valid Stake call
        var desiredStakeAmount = 10 * MinimumValidStake;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, desiredStakeAmount)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, (Timestamp)simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == desiredStakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(desiredStakeAmount == startingSoulBalance - finalSoulBalance);
            
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, (Timestamp)simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor), $"{unclaimedAmount} == {StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor)}");

        //-----------
        //Try to reduce the staked amount via Unstake function call: should fail, not enough time passed
        var initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        var stakeReduction = initialStakedAmount - MinimumValidStake;
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address, stakeReduction)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful());

        var finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();

        Assert.True(initialStakedAmount == finalStakedAmount);

        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(finalSoulBalance == startingSoulBalance);

        //-----------
        //Try to reduce staked amount below what is staked: should fail
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        stakeReduction = stakedAmount * 2;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address,
                    stakeReduction)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful()); 


        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(finalSoulBalance == startingSoulBalance);

        //-----------
        //Try a full unstake: should fail, didnt wait 24h
        initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        stakeReduction = initialStakedAmount;
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address, stakeReduction)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful()); 


        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(startingSoulBalance == finalSoulBalance);

        finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(initialStakedAmount == finalStakedAmount);

        //-----------
        //Time skip 1 day
        simulator.TimeSkipDays(1, true);
            
        //-----------
        //Try a partial unstake: should pass
        initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, (Timestamp)simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        stakeReduction = initialStakedAmount - MinimumValidStake;
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address, stakeReduction)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(stakeReduction == finalSoulBalance - startingSoulBalance, $"{stakeReduction} == {finalSoulBalance} - {startingSoulBalance}");

        finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(initialStakedAmount - finalStakedAmount == stakeReduction);

        //-----------
        //Time skip 1 day
        simulator.TimeSkipDays(1);

        //-----------
        //Try a full unstake: should pass
        initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        stakeReduction = initialStakedAmount;
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address, stakeReduction)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.True(stakeReduction == finalSoulBalance - startingSoulBalance);

        finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(initialStakedAmount - finalStakedAmount == stakeReduction);
    }
    
    [Fact]
    public void TestFreshAddressStakeClaim()
    {
        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 10;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.EndBlock();

        simulator.TimeSkipDays(1);

        //-----------
        //Perform a valid Stake & Claim call
        var desiredStake = MinimumValidStake;
        var t1 = simulator.CurrentTime;

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, desiredStake)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim),testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        var blocks = simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        BigInteger stakedAmount =
            simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == desiredStake);

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        var kcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.True(kcalBalance == BaseKCALBalance + StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost, $"{kcalBalance} == {StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost}");
        Assert.True(unclaimedAmount == 0);

        //-----------
        //Perform another claim call: should fail, not enough time passed between claim calls
        var startingFuelBalance = kcalBalance;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());


        var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.True(startingFuelBalance == finalFuelBalance);
    }
    
    [Fact]
    public void TestStakeWithSwapFeesFresh()
    {
        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 10;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.GenerateTransfer(owner, SmartContract.GetAddressForNative(NativeContractKind.Swap), nexus.RootChain, DomainSettings.StakingTokenSymbol, BaseSOULBalance);
        simulator.GenerateTransfer(owner, SmartContract.GetAddressForNative(NativeContractKind.Swap), nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.EndBlock();

        //-----------
        //Perform a valid Stake & Claim call
        var desiredStake = MinimumValidStake;

        var kcalInitialRate = simulator.InvokeContract(NativeContractKind.Swap, nameof(SwapContract.GetRate),
            DomainSettings.FuelTokenSymbol, DomainSettings.StakingTokenSymbol, MinimumValidStake).AsNumber();
        kcalInitialRate++;
        
        var kcalRate = simulator.InvokeContract(NativeContractKind.Swap, nameof(SwapContract.GetRate),
            DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, kcalInitialRate).AsNumber();

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.Minimal, () =>
            ScriptUtils.BeginScript()
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.SwapFee), testUser.Address, DomainSettings.StakingTokenSymbol, MinimumValidStake)
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 999)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, desiredStake)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim),testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        var blocks = simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);

        var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        BigInteger stakedAmount =
            simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == desiredStake);

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        var kcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.True(kcalBalance == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost + kcalRate , $"{kcalBalance} == {StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost + kcalRate}");
        Assert.True(unclaimedAmount == 0);
    }
    
    [Fact]
    public void TestClaimThenStake()
    {
        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 10;
        var minKCAL = UnitConversion.ToBigInteger(1, 10);
        
        Transaction tx = null;
        BigInteger fees = 0;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, minKCAL);
        simulator.GenerateTransfer(owner, SmartContract.GetAddressForNative(NativeContractKind.Swap), nexus.RootChain, DomainSettings.StakingTokenSymbol, BaseSOULBalance);
        simulator.GenerateTransfer(owner, SmartContract.GetAddressForNative(NativeContractKind.Swap), nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.EndBlock();

        //-----------
        //Perform a valid Claim & Stake call
        var desiredStake = MinimumValidStake;
        
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, NexusSimulator.DefaultGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, desiredStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful(), simulator.FailedTxReason);

        var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
        fees += txCost;
        
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 999)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, desiredStake)
                .SpendGas(testUser.Address)
                .EndScript());
        var blocks = simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
        fees += txCost;
        
        BigInteger stakedAmount =
            simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == desiredStake*2);

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        var kcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.True(kcalBalance == minKCAL + StakeToFuel(desiredStake, DefaultEnergyRatioDivisor) - fees , $"{kcalBalance} == {minKCAL+StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost }");
        Assert.True(unclaimedAmount == StakeToFuel(desiredStake, DefaultEnergyRatioDivisor));
    }
        
    [Fact]
    public void TestClaim()
    {
        //Let A be an address
        var testUser = PhantasmaKeys.Generate();
        testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 10;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance*2);
        simulator.EndBlock();

        simulator.TimeSkipToDate(DateTime.UtcNow);

        //-----------
        //Perform a valid Stake call
        var desiredStake = MinimumValidStake*4;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, desiredStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        BigInteger stakedAmount =
            simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == desiredStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();

        Assert.True(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

        //-----------
        //Perform a claim call: should pass
        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == desiredStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        //-----------
        //Perform another claim call: should fail, not enough time passed between claim calls
        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful());

        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        Assert.True(finalFuelBalance == startingFuelBalance);

        //-----------
        //Increase the staked amount
        var previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        var addedStake = MinimumValidStake;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, addedStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == previousStake + addedStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

        //-----------
        //Perform another claim call: should get reward only for the newly staked amount
        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address).
                SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == previousStake + addedStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        //-----------
        //Increase the staked amount a 2nd time
        previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        addedStake = MinimumValidStake * 3;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, addedStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == previousStake + addedStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

        //-----------
        //Perform another claim call: should get reward only for the newly staked amount
        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == previousStake + addedStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        //-----------
        //Perform another claim call: should fail, not enough time passed between claim calls
        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful()); 


        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        Assert.True(finalFuelBalance == startingFuelBalance);

        //-----------
        //Time skip 1 day
        simulator.TimeSkipDays(1);

        //Perform another claim call: should get reward for total staked amount
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        var expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor);
        Assert.True(unclaimedAmount == expectedUnclaimed);

        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        //-----------
        //Time skip 5 days
        var days = 5;
        simulator.TimeSkipDays(days);

        //Perform another claim call: should get reward for accumulated days
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) * days);

        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        //-----------
        //Increase the staked amount a 3rd time
        previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        addedStake = MinimumValidStake * 2;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, addedStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == previousStake + addedStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

        //-----------
        //Time skip 1 day
        days = 1;
        simulator.TimeSkipDays(days);

        //Perform another claim call: should get reward for 1 day of full stake and 1 day of partial stake
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) + StakeToFuel(addedStake, DefaultEnergyRatioDivisor);
        Assert.True(unclaimedAmount == expectedUnclaimed);

        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        //----------
        //Increase stake by X
        previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        addedStake = MinimumValidStake;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, addedStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == previousStake + addedStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

        //Time skip 1 day
        days = 1;
        simulator.TimeSkipDays(days);

        //Total unstake
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address, previousStake + addedStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        var finalStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(finalStake == 0);

        //Claim -> should get StakeToFuel(X) for same day reward and StakeToFuel(X + previous stake) due to full 1 day staking reward before unstake
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        expectedUnclaimed = StakeToFuel(addedStake, DefaultEnergyRatioDivisor);
        Console.WriteLine($"unclaimed: {unclaimedAmount} - expected {expectedUnclaimed}");
        Assert.True(unclaimedAmount == expectedUnclaimed);

        startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful());

        finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Console.WriteLine("final: " + finalFuelBalance);
        Assert.True(finalFuelBalance == startingFuelBalance);
    }
        
    [Fact]
    public void TestUnclaimedAccumulation()
    {
        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        simulator.TimeSkipToDate(DateTime.UtcNow);

        var stakeUnit = MinimumValidStake;
        var rewardPerStakeUnit = StakeToFuel(stakeUnit, DefaultEnergyRatioDivisor);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeUnit)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == stakeUnit);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == rewardPerStakeUnit);

        //-----------
        //Time skip 4 days: make sure appropriate stake reward accumulation 
        var days = 4;
        simulator.TimeSkipDays(days);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        var expectedUnclaimed = rewardPerStakeUnit * (days + 1);
        Assert.True(unclaimedAmount == expectedUnclaimed);

        //Perform another stake call
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeUnit).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        expectedUnclaimed = rewardPerStakeUnit * (days + 2);
        Assert.True(unclaimedAmount == expectedUnclaimed);
    }
        
    [Fact]
    public void TestHalving()
    {
        var startDate = simulator.CurrentTime;
        var firstBlockHash = nexus.RootChain.GetBlockHashAtHeight(nexus.RootChain.Height);

        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        //-----------
        //Perform a valid Stake call
        var desiredStakeAmount = MinimumValidStake * 10;
        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, desiredStakeAmount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == desiredStakeAmount);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

        //Time skip over 4 years and 8 days
        var firstBlock = nexus.RootChain.GetBlockByHash(firstBlockHash);

        simulator.CurrentTime = ((DateTime)firstBlock.Timestamp).AddYears(2);
        var firstHalvingDate = simulator.CurrentTime;
        var firstHalvingDayCount = (firstHalvingDate - startDate).Days;

        simulator.CurrentTime = simulator.CurrentTime.AddYears(2);
        var secondHalvingDate = simulator.CurrentTime;
        var secondHalvingDayCount = (secondHalvingDate - firstHalvingDate).Days;

        var thirdHalvingDayCount = 8;
        simulator.TimeSkipDays(thirdHalvingDayCount);

        //Validate halving
        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();

        var expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) * (1 + firstHalvingDayCount + (secondHalvingDayCount) + (thirdHalvingDayCount));
        Assert.Equal(unclaimedAmount, expectedUnclaimed);

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

        var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
        var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

        Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);
    }


    [Fact]
    public void TestVotingPower()
    {
        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 5000;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        var actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();
        Assert.True(actualVotingPower == 0);

        var MinimumVotingStake = MinimumValidStake * 1000;
        Assert.True(accountBalance >= MinimumVotingStake);

        var initialStake = MinimumVotingStake;

        //-----------
        //Perform stake operation
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, initialStake).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();
        Assert.True(actualVotingPower == initialStake);

        //-----------
        //Perform stake operation
        var addedStake = MinimumVotingStake * 2;

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, addedStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();
        Assert.True(actualVotingPower == initialStake + addedStake);

        //-----------
        //Skip 10 days
        var firstWait = 10;
        simulator.TimeSkipDays(firstWait);

        //-----------
        //Check current voting power
        BigInteger expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait));
        expectedVotingPower = expectedVotingPower / 100;
        actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();

        Assert.True(actualVotingPower == expectedVotingPower);

        //------------
        //Perform stake operation
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, addedStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        //-----------
        //Skip 5 days
        var secondWait = 5;
        simulator.TimeSkipDays(secondWait);

        //-----------
        //Check current voting power
        expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait)) + (addedStake * (100 + secondWait));
        expectedVotingPower = expectedVotingPower / 100;
        actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();

        Assert.True(actualVotingPower == expectedVotingPower);

        //-----------
        //Try a partial unstake
        var stakeReduction = MinimumVotingStake;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address, stakeReduction)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait)) / 100;
        expectedVotingPower += ((addedStake - stakeReduction) * (100 + secondWait)) / 100;
        actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();

        Assert.True(actualVotingPower == expectedVotingPower);

        //-----------
        //Try full unstake of the last stake
        var thirdWait = 1;
        simulator.TimeSkipDays(thirdWait);

        stakeReduction = addedStake - stakeReduction;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUser.Address, stakeReduction)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();

        expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait + thirdWait)) / 100;
        actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();

        Assert.True(actualVotingPower == expectedVotingPower);

        //-----------
        //Test max voting power bonus cap

        simulator.TimeSkipDays(1500);

        expectedVotingPower = ((initialStake + addedStake) * (100 + StakeContract.MaxVotingPowerBonus)) / 100;
        actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();

        Assert.True(actualVotingPower == expectedVotingPower);
    }

    [Fact]
    public void TestStaking()
    {
        var testUser = PhantasmaKeys.Generate();
        var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var accountBalance = MinimumValidStake * 100;

        Transaction tx = null;

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
        simulator.EndBlock();

        //Try to stake an amount lower than EnergyRacioDivisor
        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var initialStake = StakeContract.FuelToStake(1, DefaultEnergyRatioDivisor) - 1;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, initialStake)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful()); 


        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(finalSoulBalance == startingSoulBalance);

        //----------
        //Try to stake an amount higher than the account's balance
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, accountBalance * 10).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();
            
        Assert.False(simulator.LastBlockWasSuccessful()); 


        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == 0);

        //-----------
        //Perform a valid Stake call
        initialStake = MinimumValidStake;
        startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, initialStake).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == initialStake);


        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(initialStake == startingSoulBalance - finalSoulBalance);

        Assert.True(accountBalance == finalSoulBalance + stakedAmount);

        //-----------
        //Perform another valid Stake call
        var addedStake = MinimumValidStake * 10;
        var totalExpectedStake = initialStake + addedStake;

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, addedStake).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
        Assert.True(stakedAmount == totalExpectedStake);

        unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
        Assert.True(unclaimedAmount == StakeToFuel(totalExpectedStake, DefaultEnergyRatioDivisor));

        finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(totalExpectedStake == startingSoulBalance - finalSoulBalance);
    }

    [Fact]
    public void TestClaimWithCrown()
    {
        Filter.Test(() =>
        {

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.True(unclaimedAmount == 0);

            Transaction tx = null;

            BigInteger accountBalance = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance * 5);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserA.Address, accountBalance * 2)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            var stakeBlock = simulator.EndBlock().FirstOrDefault();
            Assert.True(simulator.LastBlockWasSuccessful());

            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.IsMaster), testUserA.Address).AsBool();
            Assert.True(isMaster);

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var rewardToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.RewardTokenSymbol);

            Assert.True(rewardToken.Symbol == DomainSettings.RewardTokenSymbol);

            //simulator.Nexus.RootChain.InvokeContractAtTimestamp()
            var stakeTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var rewardTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, rewardToken, testUserA.Address);

            var baseDate = (DateTime)simulator.CurrentTime;

            // we need two inflation events instead of one
            // The first one will always only distribute CROWNs to initial validators due to genesis time being used as first inflation period
            for (int i = 1; i <= 2; i++)
            {
                simulator.TimeSkipDays(90, false);

                var inflationBlock = simulator.TimeSkipDays(1, false);
                Assert.True(simulator.LastBlockWasSuccessful());

                var inflationHappened = false;
                Assert.True(inflationBlock.TransactionHashes.Length > 1);

                foreach (var hash in inflationBlock.TransactionHashes)
                {
                    var events = inflationBlock.GetEventsForTransaction(hash);

                    foreach (var evt in events)
                        if (evt.Kind == EventKind.Inflation)
                        {
                            inflationHappened = true;
                            break;
                        }
                }

                Assert.True(inflationHappened);

                var genesisBlock = simulator.Nexus.GetGenesisBlock();
                var genesisDiff = (DateTime)inflationBlock.Timestamp - (DateTime)genesisBlock.Timestamp;
                Assert.True(genesisDiff.TotalDays >= 90);

                var stakeDiff = (DateTime)inflationBlock.Timestamp - (DateTime)stakeBlock.Timestamp;
                Assert.True(stakeDiff.TotalDays >= 90);
            }

            var ellapsedDays = (int)(((DateTime)simulator.CurrentTime) - baseDate).TotalDays;

            var rewardTokens = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, rewardToken.Symbol, testUserA.Address);
            Assert.Equal(1, rewardTokens);

            var unclaimedAmountBefore = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();

            var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUserA.Address).AsNumber();
            var rewardTokensAmountPerDay = UnitConversion.ToDecimal(StakeContract.StakeToFuel(stakedAmount, 500), DomainSettings.FuelTokenDecimals) * (5 * (decimal)1 / 100);
            var fuelAmount = UnitConversion.ToBigInteger((ellapsedDays + 1) * UnitConversion.ToDecimal(StakeContract.StakeToFuel(stakedAmount, 500), DomainSettings.FuelTokenDecimals)
                 , DomainSettings.FuelTokenDecimals);
            // Missing master claim bonus

            Assert.Equal(unclaimedAmountBefore, fuelAmount);
            simulator.TimeSkipDays(1, false);

            fuelAmount = UnitConversion.ToBigInteger((ellapsedDays + 2) * UnitConversion.ToDecimal(StakeContract.StakeToFuel(stakedAmount, 500), DomainSettings.FuelTokenDecimals)
                , DomainSettings.FuelTokenDecimals);
            var bonus = (StakeContract.StakeToFuel(stakedAmount, 500) * 5) / 100;
            var dailyBonus = bonus;

            var unclaimedAmountAfter = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.Equal((fuelAmount + dailyBonus ), unclaimedAmountAfter);
        });
    }

    [Fact]
    public void TestSoulMaster()
    {
        Filter.Test(() =>
        {
            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.True(unclaimedAmount == 0);

            Transaction tx = null;

            BigInteger accountBalance = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //A stakes under master threshold -> verify A is not master
            var initialStake = accountBalance - MinimumValidStake;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserA.Address, initialStake)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();

            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.IsMaster), testUserA.Address).AsBool();
            Assert.True(isMaster == false);

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //A attempts master claim -> verify failure: not a master
            var startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.False(simulator.LastBlockWasSuccessful());

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.True(finalBalance == startingBalance);

            //----------
            //A stakes the master threshold -> verify A is master
            var masterAccountThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
            var missingStake = masterAccountThreshold - initialStake;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserA.Address, missingStake)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "IsMaster", testUserA.Address).AsBool();
            Assert.True(isMaster);

            //-----------
            //A attempts master claim -> verify failure: didn't wait until the 1st of the month after genesis block
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.False(simulator.LastBlockWasSuccessful());

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.True(finalBalance == startingBalance);

            //-----------
            //A attempts master claim during the first valid staking period -> verify success: rewards should be available at the end of mainnet's release month
            var missingDays = 0;

            if (simulator.CurrentTime.Month + 1 == 13)
                missingDays = (new DateTime(simulator.CurrentTime.Year + 1, 1, 1) - simulator.CurrentTime).Days;
            else
                missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;

            Console.WriteLine("before test: sim current: " + simulator.CurrentTime + " missing days: " + missingDays);
            simulator.TimeSkipDays(missingDays, true);
            simulator.TimeSkipDays(31);
            //simulator.TimeSkipHours(1);
            Console.WriteLine("after test: sim current: " + simulator.CurrentTime);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetClaimMasterCount", (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());
            
            var expectedBalance = startingBalance + (StakeContract.MasterClaimGlobalAmount / claimMasterCount) + (StakeContract.MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.True(finalBalance == expectedBalance);

            //-----------
            //A attempts master claim after another month of staking -> verify success
            if (simulator.CurrentTime.Month + 1 == 13)
                missingDays = (new DateTime(simulator.CurrentTime.Year + 1, 1, 1) - simulator.CurrentTime).Days;
            else
                missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;

            simulator.TimeSkipDays(missingDays, true);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetClaimMasterCount), (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());


            expectedBalance = startingBalance + (StakeContract.MasterClaimGlobalAmount / claimMasterCount) + (StakeContract.MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.True(finalBalance == expectedBalance);

            //-----------
            //A attempts master claim -> verify failure: not enough time passed since last claim
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.False(simulator.LastBlockWasSuccessful());

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.True(finalBalance == startingBalance);

            //-----------
            //A unstakes under master thresold -> verify lost master status
            var stakeReduction = MinimumValidStake;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Unstake), testUserA.Address, stakeReduction)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.IsMaster), testUserA.Address).AsBool();
            
            var stakedAmount  = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUserA.Address).AsNumber();
//            Assert.Fail(stakedAmount.ToString());
            Assert.Equal(false, isMaster);

            ////-----------
            ////A restakes to the master threshold -> verify won master status again
            //missingStake = masterAccountThreshold - initialStake;

            //simulator.BeginBlock();
            //tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
            //        .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserA.Address, missingStake).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "IsMaster", simulator.CurrentTime, testUserA.Address).AsBool();
            //Assert.True(isMaster);

            ////-----------
            ////Time skip to the next possible claim date
            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            ////-----------
            ////A attempts master claim -> verify failure, because he lost master status once during this reward period
            //startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //Assert.ThrowsException<ChainException>(() =>
            //{
            //    simulator.BeginBlock();
            //    simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //        ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
            //            .CallContract(NativeContractKind.Stake, "MasterClaim", testUserA.Address).
            //            SpendGas(testUserA.Address).EndScript());
            //    simulator.EndBlock();
            //});

            //finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //Assert.True(finalBalance == startingBalance);

            ////-----------
            ////Time skip to the next possible claim date
            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            ////-----------
            ////A attempts master claim -> verify success
            //startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //simulator.BeginBlock();
            //simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
            //        .CallContract(NativeContractKind.Stake, "MasterClaim", testUserA.Address).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //expectedBalance = startingBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            //finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //Assert.True(finalBalance == expectedBalance);

            ////Let B and C be other addresses
            //var testUserB = PhantasmaKeys.Generate();
            //var testUserC = PhantasmaKeys.Generate();

            //simulator.BeginBlock();
            //simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            //simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            //simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            //simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            //simulator.EndBlock();

            ////----------
            ////B and C stake the master threshold -> verify both become masters

            //simulator.BeginBlock();
            //tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
            //        .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserB.Address, accountBalance).
            //        SpendGas(testUserB.Address).EndScript());
            //simulator.EndBlock();

            //simulator.BeginBlock();
            //tx = simulator.GenerateCustomTransaction(testUserC, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserC.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
            //        .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserC.Address, accountBalance).
            //        SpendGas(testUserC.Address).EndScript());
            //simulator.EndBlock();

            //isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "IsMaster", simulator.CurrentTime, testUserB.Address).AsBool();
            //Assert.True(isMaster);

            //isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "IsMaster", simulator.CurrentTime, testUserC.Address).AsBool();
            //Assert.True(isMaster);

            ////----------
            ////Confirm that B and C should only receive master claim rewards on the 2nd closest claim date

            //var closeClaimDate = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetMasterClaimDate", simulator.CurrentTime, 1).AsTimestamp();
            //var farClaimDate = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetMasterClaimDate", simulator.CurrentTime, 2).AsTimestamp();

            //var closeClaimMasters = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetClaimMasterCount", simulator.CurrentTime, closeClaimDate).AsNumber();
            //var farClaimMasters = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetClaimMasterCount", simulator.CurrentTime, farClaimDate).AsNumber();
            //Assert.True(closeClaimMasters == 1 && farClaimMasters == 3);

            ////----------
            ////Confirm in fact that only A receives rewards on the closeClaimDate

            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            //var startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //var startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //var startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //simulator.BeginBlock();
            //simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
            //        .CallContract(NativeContractKind.Stake, "MasterClaim", testUserA.Address).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //expectedBalance = startingBalanceA + (MasterClaimGlobalAmount / closeClaimMasters) + (MasterClaimGlobalAmount % closeClaimMasters);
            //var finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //var finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //var finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //Assert.True(finalBalanceA == expectedBalance);
            //Assert.True(finalBalanceB == startingBalanceB);
            //Assert.True(finalBalanceC == startingBalanceC);

            ////----------
            ////Confirm in fact that A, B and C receive rewards on the farClaimDate

            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            //startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //simulator.BeginBlock();
            //simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
            //        .CallContract(NativeContractKind.Stake, "MasterClaim", testUserA.Address).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //var expectedBalanceA = startingBalanceA + (MasterClaimGlobalAmount / farClaimMasters) + (MasterClaimGlobalAmount % farClaimMasters);
            //var expectedBalanceB = startingBalanceB + (MasterClaimGlobalAmount / farClaimMasters);
            //var expectedBalanceC = startingBalanceC + (MasterClaimGlobalAmount / farClaimMasters);


            //finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //Assert.True(finalBalanceA == expectedBalanceA);
            //Assert.True(finalBalanceB == expectedBalanceB);
            //Assert.True(finalBalanceC == expectedBalanceC);
        });
    }

    [Fact]
    public void TestBigStakes()
    {
        Filter.Test(() =>
        {
            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.True(unclaimedAmount == 0);

            Transaction tx = null;

            var masterAccountThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
            BigInteger accountBalance = 2 * masterAccountThreshold;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            //----------
            //A stakes twice the master threshold -> verify A is master

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserA.Address, accountBalance)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();

            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.IsMaster), testUserA.Address).AsBool();
            Assert.True(isMaster);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

            //-----------
            //Perform a claim call: should pass
            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            var expectedUnclaimed = StakeToFuel(accountBalance, DefaultEnergyRatioDivisor);
            Assert.True(unclaimedAmount == expectedUnclaimed);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUserA.Address, testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.True(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUserA.Address).AsNumber();
            Assert.True(stakedAmount == accountBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.True(unclaimedAmount == 0);

            //-----------
            //Time skip to the next possible claim date

            var missingDays = 30;
            // (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1)).Day; //.Subtract(simulator.CurrentTime).Days + 1; // - simulator.CurrentTime).Days + 1;
            simulator.TimeSkipDays(missingDays + 1, true);
            simulator.TimeSkipDays(31);

            //-----------
            //A attempts master claim -> verify success
            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetClaimMasterCount), (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            Assert.True(simulator.LastBlockWasSuccessful());

            var expectedSoulBalance = startingSoulBalance + (StakeContract.MasterClaimGlobalAmount / claimMasterCount) + (StakeContract.MasterClaimGlobalAmount % claimMasterCount);
            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.True(finalSoulBalance == expectedSoulBalance, $"{finalSoulBalance} == {expectedSoulBalance}");
        });
    }
        
    [Fact]
    public void TestFuelStakeConversion()
    {
        var stake = 100;
        var fuel = StakeToFuel(stake, DefaultEnergyRatioDivisor);
        var stake2 = StakeContract.FuelToStake(fuel, DefaultEnergyRatioDivisor);
        //20, 10, 8
        Console.WriteLine($"{stake2} - {fuel}: {UnitConversion.ConvertDecimals(fuel, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals)}");
        Assert.True(stake == stake2);
    }


    [Fact]
    public void TestMasterClaim()
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
            StakeContract.DefaultMasterThreshold * 2);
        simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol,
            UnitConversion.ToBigInteger(10, DomainSettings.FiatTokenDecimals));
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        // Stake
        simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, StakeContract.DefaultMasterThreshold)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        simulator.GetFundsInTheFuture(owner);
        Assert.True(simulator.LastBlockWasSuccessful());

        // Get the balance
        var accountBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
        Assert.Equal(StakeContract.DefaultMasterThreshold + initialAmount, accountBalance );
        
        // Trigger the master claim
        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), user.Address)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var masterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetMasterCount), (Timestamp)simulator.CurrentTime).AsNumber();
        Assert.Equal(3, masterCount);

        // Get the balance
        accountBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, user.Address);
        Assert.Equal(StakeContract.DefaultMasterThreshold + initialAmount + StakeContract.MasterClaimGlobalAmount/masterCount + StakeContract.MasterClaimGlobalAmount%masterCount, accountBalance );
    }
    
    
    // GetMasterCount
    // GetMasterAddresses
    // GetMasterRewards
    // GetTimeBeforeUnstake
    // GetStakeTimestamp
    // FuelToStake
    
    
}
