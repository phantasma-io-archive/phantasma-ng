using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Simulator;

namespace Phantasma.LegacyTests.ContractTests;

[TestClass]
public class StakeContractTest
{
    public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);
    public static BigInteger MinimumGasLimit = 9999999;
    public static BigInteger BaseKCALBalance = UnitConversion.ToBigInteger(100, 10);

    public uint DefaultEnergyRatioDivisor => StakeContract.DefaultEnergyRatioDivisor;

    public BigInteger StakeToFuel(BigInteger stakeAmount, uint _currentEnergyRatioDivisor)
    {
        return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _currentEnergyRatioDivisor;
    }
    
    [TestMethod]
    public void TestEnergyRatioDecimals()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

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

        Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

        BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
        Assert.IsTrue(actualEnergyRatio == DefaultEnergyRatioDivisor);
    }
    
    [TestMethod]
    public void TestGetUnclaimed()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

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

        var unclaimedValue = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake,
            nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();

        Assert.IsTrue(unclaimedValue == expectedUnclaimedAmount, $"{unclaimedValue} == {expectedUnclaimedAmount}");
    }
    
    [TestMethod]
    public void TestUnstake()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);
            
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, (Timestamp)simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor), $"{unclaimedAmount} == {StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor)}");

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
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful()); 
            

            var finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();

            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

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
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful()); 


            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

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
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful()); 


            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(startingSoulBalance == finalSoulBalance);

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

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
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance, $"{stakeReduction} == {finalSoulBalance} - {startingSoulBalance}");

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);

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
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance);

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);
        }
    
     [TestMethod]
        public void TestFreshAddressStakeClaim()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;
            

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 10;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            //simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
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
            Assert.IsFalse(simulator.LastBlockWasSuccessful());

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            BigInteger stakedAmount =
                simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            var kcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.IsTrue(kcalBalance == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost, $"{kcalBalance} == {StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost}");
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Perform another claim call: should fail, not enough time passed between claim calls
            var startingFuelBalance = kcalBalance;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful());


            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.IsTrue(startingFuelBalance == finalFuelBalance);
        }
        
        [TestMethod]
        public void TestClaim()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            //Let A be an address
            var testUser = PhantasmaKeys.Generate();
            testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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

             BigInteger stakedAmount =
                simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();

            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

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
            
            Assert.IsTrue(simulator.LastBlockWasSuccessful());

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful());

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);

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
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

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
            Assert.IsTrue(simulator.LastBlockWasSuccessful());

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            Assert.IsTrue(simulator.LastBlockWasSuccessful());

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

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
            Assert.IsTrue(simulator.LastBlockWasSuccessful());

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful()); 


            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Perform another claim call: should get reward for total staked amount
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            var expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

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

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip 5 days
            var days = 5;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for accumulated days
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) * days);

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

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

            //-----------
            //Time skip 1 day
            days = 1;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for 1 day of full stake and 1 day of partial stake
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) + StakeToFuel(addedStake, DefaultEnergyRatioDivisor);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

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

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

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
            Assert.IsTrue(finalStake == 0);

            //Claim -> should get StakeToFuel(X) for same day reward and StakeToFuel(X + previous stake) due to full 1 day staking reward before unstake
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            expectedUnclaimed = StakeToFuel(addedStake, DefaultEnergyRatioDivisor);
            Console.WriteLine($"unclaimed: {unclaimedAmount} - expected {expectedUnclaimed}");
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address)
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful());

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Console.WriteLine("final: " + finalFuelBalance);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);
        }
        
        [TestMethod]
        public void TestUnclaimedAccumulation()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            Assert.IsTrue(stakedAmount == stakeUnit);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == rewardPerStakeUnit);

            //-----------
            //Time skip 4 days: make sure appropriate stake reward accumulation 
            var days = 4;
            simulator.TimeSkipDays(days);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            var expectedUnclaimed = rewardPerStakeUnit * (days + 1);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

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
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);
        }
        
        [TestMethod]
        public void TestHalving()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();


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

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

            //Time skip over 4 years and 8 days
            var startDate = simulator.CurrentTime;
            var firstBlockHash = nexus.RootChain.GetBlockHashAtHeight(1);
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
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);
        }


        [TestMethod]
        public void TestVotingPower()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 5000;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();
            Assert.IsTrue(actualVotingPower == 0);

            var MinimumVotingStake = MinimumValidStake * 1000;
            Assert.IsTrue(accountBalance >= MinimumVotingStake);

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
            Assert.IsTrue(actualVotingPower == initialStake);

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
            Assert.IsTrue(actualVotingPower == initialStake + addedStake);

            //-----------
            //Skip 10 days
            var firstWait = 10;
            simulator.TimeSkipDays(firstWait);

            //-----------
            //Check current voting power
            BigInteger expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait));
            expectedVotingPower = expectedVotingPower / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

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

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

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

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

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

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Test max voting power bonus cap

            simulator.TimeSkipDays(1500);

            expectedVotingPower = ((initialStake + addedStake) * (100 + StakeContract.MaxVotingPowerBonus)) / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetAddressVotingPower), testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);
        }

        [TestMethod]
        public void TestStaking()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful()); 


            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //----------
            //Try to stake an amount higher than the account's balance
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, accountBalance * 10).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful()); 


            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            Assert.IsTrue(stakedAmount == initialStake);


            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(initialStake == startingSoulBalance - finalSoulBalance);

            Assert.IsTrue(accountBalance == finalSoulBalance + stakedAmount);

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
            Assert.IsTrue(stakedAmount == totalExpectedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(totalExpectedStake, DefaultEnergyRatioDivisor));

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(totalExpectedStake == startingSoulBalance - finalSoulBalance);
        }

        [TestMethod]
        public void TestClaimWithCrown()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(new []{owner});
            var nexus = simulator.Nexus;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            Transaction tx = null;

            BigInteger accountBalance = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance*5);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUserA.Address, accountBalance*2)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            var stakeBlock = simulator.EndBlock().FirstOrDefault();
            Assert.IsTrue(simulator.LastBlockWasSuccessful());
            
            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.IsMaster), testUserA.Address).AsBool();
            Assert.IsTrue(isMaster);

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var rewardToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.RewardTokenSymbol);

            Assert.IsTrue(rewardToken.Symbol == DomainSettings.RewardTokenSymbol);
            
            var stakeTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var rewardTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, rewardToken, testUserA.Address);

            var baseDate = (DateTime) simulator.CurrentTime;

            // we need two inflation events instead of one
            // The first one will always only distribute CROWNs to initial validators due to genesis time being used as first inflation period
            for (int i=1; i<=2; i++)
            {
                simulator.TimeSkipDays(90, false);

                var inflationBlock = simulator.TimeSkipDays(1, false);
                Assert.IsTrue(simulator.LastBlockWasSuccessful());

                var inflationHappened = false;
                Assert.IsTrue(inflationBlock.TransactionHashes.Length > 1);

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

                Assert.IsTrue(inflationHappened);

                var genesisBlock = simulator.Nexus.GetGenesisBlock();
                var genesisDiff = (DateTime)inflationBlock.Timestamp - (DateTime)genesisBlock.Timestamp;
                Assert.IsTrue(genesisDiff.TotalDays >= 90);

                var stakeDiff = (DateTime)inflationBlock.Timestamp - (DateTime)stakeBlock.Timestamp;
                Assert.IsTrue(stakeDiff.TotalDays >= 90);
            }

            var ellapsedDays = (int)(((DateTime) simulator.CurrentTime) - baseDate).TotalDays;


            rewardTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, rewardToken, testUserA.Address);
            Assert.IsTrue(rewardTokenBalance == 1, $"{rewardTokenBalance} == 1");

            var unclaimedAmountBefore = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();

            var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUserA.Address).AsNumber();
            var fuelAmount = StakeContract.StakeToFuel(stakedAmount, 500) * (ellapsedDays + 1);

            Assert.IsTrue(fuelAmount == unclaimedAmountBefore);
            simulator.TimeSkipDays(1, false);

            fuelAmount = StakeContract.StakeToFuel(stakedAmount, 500) * (ellapsedDays + 2);
            var bonus = (fuelAmount * 5) / 100; 
            var dailyBonus = bonus / (ellapsedDays+2);

            var unclaimedAmountAfter = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.IsTrue((fuelAmount + dailyBonus) == unclaimedAmountAfter, $"{(fuelAmount + dailyBonus)} == {unclaimedAmountAfter}");
        }

        [TestMethod]
        public void TestSoulMaster()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

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
            Assert.IsTrue(isMaster == false);

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

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

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

            isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "IsMaster", testUserA.Address).AsBool();
            Assert.IsTrue(isMaster);

            //-----------
            //A attempts master claim -> verify failure: didn't wait until the 1st of the month after genesis block
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful()); 

            

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //A attempts master claim during the first valid staking period -> verify success: rewards should be available at the end of mainnet's release month
            var missingDays = 0;

            if ( simulator.CurrentTime.Month + 1 == 13)
                missingDays = (new DateTime(simulator.CurrentTime.Year+1 ,  1, 1) - simulator.CurrentTime).Days;
            else
                missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            
            Console.WriteLine("before test: sim current: " + simulator.CurrentTime + " missing days: " + missingDays);
            simulator.TimeSkipDays(missingDays, true);
            //simulator.TimeSkipHours(1);
            Console.WriteLine("after test: sim current: " + simulator.CurrentTime);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetClaimMasterCount", (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();


            var expectedBalance = startingBalance + (StakeContract.MasterClaimGlobalAmount / claimMasterCount) + (StakeContract.MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == expectedBalance);

            //-----------
            //A attempts master claim after another month of staking -> verify success
            if ( simulator.CurrentTime.Month + 1 == 13)
                missingDays = (new DateTime(simulator.CurrentTime.Year+1 ,  1, 1) - simulator.CurrentTime).Days;
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


            expectedBalance = startingBalance + (StakeContract.MasterClaimGlobalAmount / claimMasterCount) + (StakeContract.MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == expectedBalance);

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
            
            Assert.IsFalse(simulator.LastBlockWasSuccessful());

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

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

            isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.IsMaster), testUserA.Address).AsBool();
            Assert.IsTrue(isMaster == false);

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
            //Assert.IsTrue(isMaster);

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

            //Assert.IsTrue(finalBalance == startingBalance);

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

            //Assert.IsTrue(finalBalance == expectedBalance);

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
            //Assert.IsTrue(isMaster);

            //isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "IsMaster", simulator.CurrentTime, testUserC.Address).AsBool();
            //Assert.IsTrue(isMaster);

            ////----------
            ////Confirm that B and C should only receive master claim rewards on the 2nd closest claim date

            //var closeClaimDate = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetMasterClaimDate", simulator.CurrentTime, 1).AsTimestamp();
            //var farClaimDate = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetMasterClaimDate", simulator.CurrentTime, 2).AsTimestamp();

            //var closeClaimMasters = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetClaimMasterCount", simulator.CurrentTime, closeClaimDate).AsNumber();
            //var farClaimMasters = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetClaimMasterCount", simulator.CurrentTime, farClaimDate).AsNumber();
            //Assert.IsTrue(closeClaimMasters == 1 && farClaimMasters == 3);

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

            //Assert.IsTrue(finalBalanceA == expectedBalance);
            //Assert.IsTrue(finalBalanceB == startingBalanceB);
            //Assert.IsTrue(finalBalanceC == startingBalanceC);

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

            //Assert.IsTrue(finalBalanceA == expectedBalanceA);
            //Assert.IsTrue(finalBalanceB == expectedBalanceB);
            //Assert.IsTrue(finalBalanceC == expectedBalanceC);
        }

        [TestMethod]
        public void TestBigStakes()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            Transaction tx = null;

            var masterAccountThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
            BigInteger accountBalance = 2 * masterAccountThreshold;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, BaseKCALBalance);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //----------
            //A stakes twice the master threshold -> verify A is master
            
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake) , testUserA.Address, accountBalance)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();

            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.IsMaster), testUserA.Address).AsBool();
            Assert.IsTrue(isMaster);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

            //-----------
            //Perform a claim call: should pass
            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            var expectedUnclaimed = StakeToFuel(accountBalance, DefaultEnergyRatioDivisor);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), testUserA.Address, testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetStake), testUserA.Address).AsNumber();
            Assert.IsTrue(stakedAmount == accountBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip to the next possible claim date
            var missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days + 1;
            simulator.TimeSkipDays(missingDays, true);

            //-----------
            //A attempts master claim -> verify success
            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime,  NativeContractKind.Stake, nameof(StakeContract.GetClaimMasterCount), (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, MinimumGasLimit)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.MasterClaim), testUserA.Address)
                    .SpendGas(testUserA.Address)
                    .EndScript());
            simulator.EndBlock();

            var expectedSoulBalance = startingSoulBalance + (StakeContract.MasterClaimGlobalAmount / claimMasterCount) + (StakeContract.MasterClaimGlobalAmount % claimMasterCount);
            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalSoulBalance == expectedSoulBalance, $"{finalSoulBalance} == {expectedSoulBalance}");
        }
        
        [TestMethod]
        public void TestFuelStakeConversion()
        {
            var stake = 100;
            var fuel = StakeToFuel(stake, DefaultEnergyRatioDivisor);
            var stake2 = StakeContract.FuelToStake(fuel, DefaultEnergyRatioDivisor);
            //20, 10, 8
            Console.WriteLine($"{stake2} - {fuel}: {UnitConversion.ConvertDecimals(fuel, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals)}");
            Assert.IsTrue(stake == stake2);
        }
    
    
}
