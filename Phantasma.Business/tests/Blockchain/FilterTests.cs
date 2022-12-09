using System;
using System.Linq;
using System.Xml.Linq;
using Phantasma.Business.Blockchain;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;

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
        simulator.GenerateTransfer(sender, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var oracle = nexus.GetOracleReader();
        var price = UnitConversion.ToDecimal(oracle.ReadPrice(simulator.CurrentTime, DomainSettings.StakingTokenSymbol), DomainSettings.StakingTokenDecimals);
        Assert.True(price > 0);

        var total = (int)(Filter.Quota / price) + 1;
        var transferAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals) * total;
        Assert.True(transferAmount > 0);

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUser.Address);
        Assert.True(hashes.Length == 1);

        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
        var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        Assert.True(finalBalance == 0);

        Assert.True(Filter.IsRedFilteredAddress(nexus.RootStorage, sender.Address));
        Assert.False(Filter.IsRedFilteredAddress(nexus.RootStorage, testUser.Address));
    }

}
