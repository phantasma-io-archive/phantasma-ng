using System;
using System.Linq;
using System.Xml.Linq;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Xunit;

namespace Phantasma.Business.Tests.Blockchain;

[Collection(nameof(SystemTestCollectionDefinition))]
public class FilterTests
{
    [Fact]
    public void SimpleFilter()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser = PhantasmaKeys.Generate();

        var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);

        var sender = owner;

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol,
            fuelAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var oracle = nexus.GetOracleReader();
        var price = UnitConversion.ToDecimal(
            oracle.ReadPrice(simulator.CurrentTime, DomainSettings.StakingTokenSymbol),
            DomainSettings.StakingTokenDecimals);
        Assert.True(price > 0);

        var total = (int)(Filter.Quota / price) + 1;
        var transferAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals) * total;
        Assert.True(transferAmount > 0);

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
            transferAmount);
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUser.Address);
        Assert.True(hashes.Length == 1);

        var stakeToken =
            simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var finalBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(finalBalance == 0);

        Assert.True(Filter.IsRedFilteredAddress(nexus.RootStorage, sender.Address));
        Assert.False(Filter.IsRedFilteredAddress(nexus.RootStorage, testUser.Address));
    }

    [Fact]
    public void TestRemoveFilter()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser = PhantasmaKeys.Generate();

        var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        
        var smallAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        var sender = owner;

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol,
            fuelAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, owner.Address);

        var oracle = nexus.GetOracleReader();
        var price = UnitConversion.ToDecimal(
            oracle.ReadPrice(simulator.CurrentTime, DomainSettings.StakingTokenSymbol),
            DomainSettings.StakingTokenDecimals);
        Assert.True(price > 0);

        var total = (int)(Filter.Quota / price) + 1;
        var transferAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals) * total;
        Assert.True(transferAmount > 0);

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
            transferAmount);
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUser.Address);
        Assert.True(hashes.Length == 1);

        var stakeToken =
            simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var finalBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(finalBalance == 0);

        Assert.True(Filter.IsRedFilteredAddress(nexus.RootStorage, sender.Address));
        Assert.False(Filter.IsRedFilteredAddress(nexus.RootStorage, testUser.Address));

        Filter.RemoveRedFilteredAddress(simulator.Nexus.RootStorage, sender.Address, "User X");

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
            smallAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        finalBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, owner.Address);
        
        Assert.Equal(initialBalance - smallAmount, finalBalance);
    }

    [Fact]
    public void TestAddRedFilteGreenFilter()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser = PhantasmaKeys.Generate();

        var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
        
        var smallAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        var sender = owner;

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol,
            fuelAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        Filter.AddGreenFilteredAddress(simulator.Nexus.RootStorage, testUser.Address);

        var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, owner.Address);

        var oracle = nexus.GetOracleReader();
        var price = UnitConversion.ToDecimal(
            oracle.ReadPrice(simulator.CurrentTime, DomainSettings.StakingTokenSymbol),
            DomainSettings.StakingTokenDecimals);
        Assert.True(price > 0);

        var total = (int)(Filter.Quota / price) + 1;
        var transferAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals) * total;
        Assert.True(transferAmount > 0);

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
            transferAmount);
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUser.Address);
        Assert.True(hashes.Length == 1);

        var stakeToken =
            simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var finalBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(finalBalance == 0);

        Assert.True(Filter.IsRedFilteredAddress(nexus.RootStorage, sender.Address));
        Assert.False(Filter.IsRedFilteredAddress(nexus.RootStorage, testUser.Address));

        Filter.AddGreenFilteredAddress(simulator.Nexus.RootStorage, sender.Address);
        Filter.AddRedFilteredAddress(simulator.Nexus.RootStorage, testUser.Address);

        
        Assert.True(Filter.IsGreenFilteredAddress(nexus.RootStorage, sender.Address));
        Assert.False(Filter.IsGreenFilteredAddress(nexus.RootStorage, testUser.Address));

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
            smallAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        finalBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, owner.Address);
        
        Assert.Equal(initialBalance - smallAmount, finalBalance);
    }

    [Fact]
    public void FilterQuotaReached()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser = PhantasmaKeys.Generate();

        var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);

        var sender = owner;

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol,
            fuelAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var oracle = nexus.GetOracleReader();
        var price = UnitConversion.ToDecimal(
            oracle.ReadPrice(simulator.CurrentTime, DomainSettings.StakingTokenSymbol),
            DomainSettings.StakingTokenDecimals);
        Assert.True(price > 0);

        var total = (int)(Filter.Quota / price);

        var totalSplits = 5;

        var split = total / totalSplits;

        for (int i=0; i < totalSplits; i++)
        {
            BigInteger transferAmount = split;

            if (i == totalSplits - 1)
            {
                transferAmount++; // in the last one we try to go over the quota by transfering 1 token more
            }

            transferAmount *= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);
            Assert.True(transferAmount > 0);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
                transferAmount);
            simulator.EndBlock();

            if (i == totalSplits - 1) // only last is expected to fail
            {
                Assert.False(simulator.LastBlockWasSuccessful());
            }
            else
            {
                Assert.True(simulator.LastBlockWasSuccessful());
            }
        }

        var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUser.Address);
        Assert.True(hashes.Length == totalSplits);

        BigInteger expectedBalance = split * (totalSplits - 1); // last one is supposed to fail
        expectedBalance *= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        var stakeToken =
            simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var finalBalance =
            simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(finalBalance == expectedBalance);

        Assert.True(Filter.IsRedFilteredAddress(nexus.RootStorage, sender.Address));
        Assert.False(Filter.IsRedFilteredAddress(nexus.RootStorage, testUser.Address));
    }

    [Fact]
    public void FilterQuotaNotReached()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;
        
        simulator.GetFundsInTheFuture(owner);
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
        
        // set the price of SOUL temporarily to 3 dollars
        simulator.UpdateOraclePrice(DomainSettings.StakingTokenSymbol, 3);

        var testUser = PhantasmaKeys.Generate();

        var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);

        var sender = owner;

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol,
            fuelAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var oracle = nexus.GetOracleReader();
        var price = UnitConversion.ToDecimal(
            oracle.ReadPrice(simulator.CurrentTime, DomainSettings.StakingTokenSymbol),
            DomainSettings.StakingTokenDecimals);
        Assert.True(price > 0);

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var total = (int)(Filter.Quota / price);

        var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, owner.Address);
        var initialWorth = UnitConversion.ToDecimal(initialBalance, DomainSettings.StakingTokenDecimals);
        Assert.True(initialWorth >= total);

        var totalSplits = 3;

        var split = total / totalSplits;

        for (int i = 0; i < totalSplits; i++)
        {
            BigInteger transferAmount = split;

            if (i == totalSplits - 1)
            {
                transferAmount++; // in the last one we try to go over the quota by transfering 1 token more

                simulator.TimeSkipDays(1);
            }

            transferAmount *= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);
            Assert.True(transferAmount > 0);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol,
                transferAmount);
            simulator.EndBlock();

            Assert.True(simulator.LastBlockWasSuccessful());
        }

        BigInteger minimulExpectedBalance = split * totalSplits; // last one is not supposed to fail
        minimulExpectedBalance *= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(finalBalance >= minimulExpectedBalance);

        Assert.False(Filter.IsRedFilteredAddress(nexus.RootStorage, sender.Address));
        Assert.False(Filter.IsRedFilteredAddress(nexus.RootStorage, testUser.Address));
    }

    [Fact]
    public void AddRemoveMultipleTimes()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser = PhantasmaKeys.Generate();

        var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);

        var sender = owner;

        simulator.BeginBlock();
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol,
            fuelAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());
        
        Filter.AddRedFilteredAddress(nexus.RootStorage, SmartContract.GetAddressForNative(NativeContractKind.Stake));
        Filter.RemoveRedFilteredAddress(nexus.RootStorage, SmartContract.GetAddressForNative(NativeContractKind.Stake), "filter.red");
        Filter.RemoveRedFilteredAddress(nexus.RootStorage, SmartContract.GetAddressForNative(NativeContractKind.Stake), "filter.red");
        Filter.RemoveRedFilteredAddress(nexus.RootStorage, SmartContract.GetAddressForNative(NativeContractKind.Stake), "filter.red");
        
    }

}
