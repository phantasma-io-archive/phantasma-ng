using System;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Infrastructure.Pay.Chains;
using Phantasma.Simulator;

namespace Phantasma.LegacyTests.ContractTests;

[TestClass]
public class SwapContractTest
{
    [TestMethod]
    public void TestSwaping()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var testUser = PhantasmaKeys.Generate();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 9999)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.StakingTokenSymbol, 100000000)
                .CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.FuelTokenSymbol, 10000000000)
                .SpendGas(owner.Address)
                .EndScript());
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000);
        simulator.EndBlock();

        var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
        var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

        var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var startingKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        BigInteger swapAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals) / 100;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                .CallContract("swap", "SwapTokens", testUser.Address, DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, swapAmount)
                .SpendGas(testUser.Address)
                .EndScript());
        simulator.EndBlock();
        
        var currentSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
        var currentKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

        Assert.IsTrue(currentSoulBalance < startingSoulBalance, $"{currentSoulBalance} < {startingSoulBalance}");
        Assert.IsTrue(currentKcalBalance > startingKcalBalance);
    }
    
    
    
    /*
        [TestMethod]
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

            Assert.IsTrue(targetRate == 5m);
        }*/

}
