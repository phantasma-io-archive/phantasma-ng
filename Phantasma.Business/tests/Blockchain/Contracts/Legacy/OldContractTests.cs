using Xunit;

using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.VM.Utils;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM;

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection("OldContractTests")]
public class OldContractTests
{
    public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

    public enum CustomEvent
    {
        None,
        Stuff = 20,
    }

    public uint DefaultEnergyRatioDivisor => StakeContract.DefaultEnergyRatioDivisor;

    public BigInteger StakeToFuel(BigInteger stakeAmount, uint _currentEnergyRatioDivisor)
    {
        return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _currentEnergyRatioDivisor;
    }


    [Fact]
    public void CustomEvents()
    {
        var A = CustomEvent.Stuff;
        EventKind evt = DomainExtensions.EncodeCustomEvent(A);
        var B = evt.DecodeCustomEvent<CustomEvent>();
        Assert.True(A == B);
    }
    
    /*        [Fact]
            public void TestProxies()
            {
                var owner = PhantasmaKeys.Generate();

                var simulator = new NexusSimulator(owner);
                var nexus = simulator.Nexus;

                var testUser = PhantasmaKeys.Generate();
                var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(unclaimedAmount == 0);

                var accountBalance = MinimumValidStake * 100;

                Transaction tx = null;

                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
                simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
                simulator.EndBlock();

                //-----------
                //Perform a valid Stake call
                var initialStake = MinimumValidStake;

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, initialStake).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(stakedAmount == initialStake);

                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

                //-----------
                //Add main account as proxy to itself: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, testUser.Address, 50).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                var proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 0);

                //-----------
                //Add 0% proxy: should fail
                var proxyA = PhantasmaKeys.Generate();
                var proxyAPercentage = 25;

                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, proxyA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
                simulator.EndBlock();

                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyA.Address, 0).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 0);

                var api = new NexusAPI(nexus);
                var script = ScriptUtils.BeginScript().CallContract(NativeContractKind.Stake, "GetProxies", testUser.Address).EndScript();
                var apiResult = api.InvokeRawScript("main", Base16.Encode(script));

                //-----------
                //Add and remove 90% proxy: should pass
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyA.Address, 90).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);
                Assert.True(proxyList[0].percentage == 90);

                apiResult = api.InvokeRawScript("main", Base16.Encode(script));

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "RemoveProxy", testUser.Address, proxyA.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 0);

                //-----------
                //Add and remove 100% proxy: should pass
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyA.Address, 100).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);
                Assert.True(proxyList[0].percentage == 100);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "RemoveProxy", testUser.Address, proxyA.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 0);

                //-----------
                //Add 101% proxy: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyA.Address, 101).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 0);

                //-----------
                //Add 25% proxy A: should pass
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyA.Address, proxyAPercentage).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);
                Assert.True(proxyList[0].percentage == 25);

                //-----------
                //Re-add proxy A: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyA.Address, 25).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);

                //-----------
                //Add an 80% proxy: should fail
                var proxyB = PhantasmaKeys.Generate();

                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, proxyB.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
                simulator.EndBlock();

                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyB.Address, 80).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);

                //-----------
                //Add 25% proxy B and remove it: should pass
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyB.Address, 25).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 2);
                Assert.True(proxyList[0].percentage == 25 && proxyList[1].percentage == 25);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "RemoveProxy", testUser.Address, proxyB.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);

                //-----------
                //Add 75% proxy B and remove it: should pass
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyB.Address, 75).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 2);
                Assert.True(proxyList[0].percentage == 25 && proxyList[1].percentage == 75);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "RemoveProxy", testUser.Address, proxyB.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);

                //-----------
                //Try to remove proxy B again: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "RemoveProxy", testUser.Address, proxyB.Address).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);

                //-----------
                //Add 76% proxy B: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyB.Address, 76).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);

                //Try to claim from main: should pass
                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

                var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

                var startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                var startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();

                var finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                var finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
                var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

                var proxyQuota = proxyAPercentage * unclaimedAmount / 100;
                var leftover = unclaimedAmount - proxyQuota;

                Assert.True(finalMainFuelBalance == (startingMainFuelBalance + leftover - txCost));
                Assert.True(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota));

                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(unclaimedAmount == 0);

                //-----------
                //Try to claim from main: should fail, less than 24h since last claim
                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(unclaimedAmount == 0);

                var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "Claim", testUser.Address, testUser.Address).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                Assert.True(startingFuelBalance == finalFuelBalance);

                //-----------
                //Try to claim from proxy A: should fail, less than 24h since last claim
                startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                            .EndScript());
                    simulator.EndBlock();
                });

                finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

                Assert.True(startingMainFuelBalance == finalMainFuelBalance);
                Assert.True(startingProxyFuelBalance == finalProxyFuelBalance);

                //-----------
                //Time skip 1 day
                simulator.TimeSkipDays(1);

                //Try to claim from proxy A: should pass, and the proxy should earn some fuel
                var desiredFuelClaim = StakeToFuel(initialStake, DefaultEnergyRatioDivisor);
                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

                var expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor);
                Assert.True(unclaimedAmount == expectedUnclaimed);

                startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();

                finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
                txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

                proxyQuota = proxyAPercentage * unclaimedAmount / 100;
                leftover = unclaimedAmount - proxyQuota;

                Assert.True(proxyQuota == proxyAPercentage * desiredFuelClaim / 100);
                Assert.True(desiredFuelClaim == unclaimedAmount);

                Assert.True(finalMainFuelBalance == (startingMainFuelBalance + leftover));
                Assert.True(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota - txCost));

                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(unclaimedAmount == 0);

                //-----------
                //Remove proxy A
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "RemoveProxy", testUser.Address, proxyA.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                //-----------
                //Try to claim from proxy A: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                            .EndScript());
                    simulator.EndBlock();
                });

                //-----------
                //Try to claim from main: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "Claim", testUser.Address, testUser.Address).
                            SpendGas(testUser.Address).EndScript());
                    simulator.EndBlock();
                });

                //-----------
                //Time skip 1 day
                simulator.TimeSkipDays(1);

                //Try to claim from proxy A: should fail
                Assert.ThrowsException<ChainException>(() =>
                {
                    simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, simulator.MinimumFee, 9999)
                            .CallContract(NativeContractKind.Stake, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                            .EndScript());
                    simulator.EndBlock();
                });

                //-----------
                //Try to claim from main: should pass, check removed proxy received nothing
                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();

                startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();

                finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
                txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

                Assert.True(finalMainFuelBalance == (startingMainFuelBalance + unclaimedAmount - txCost));
                Assert.True(finalProxyFuelBalance == startingProxyFuelBalance);

                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(unclaimedAmount == 0);

                //-----------
                //Add 25% proxy A: should pass
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "AddProxy", testUser.Address, proxyA.Address, proxyAPercentage).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();

                proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
                Assert.True(proxyList.Length == 1);
                Assert.True(proxyList[0].percentage == 25);

                //-----------
                //Time skip 5 days
                var days = 5;
                simulator.TimeSkipDays(days);

                //Try to claim from main: should pass, check claimed amount is from 5 days worth of accumulation
                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

                Assert.True(unclaimedAmount == days * StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

                startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                        .CallContract(NativeContractKind.Stake, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();

                finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
                finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
                txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

                proxyQuota = proxyAPercentage * unclaimedAmount / 100;
                leftover = unclaimedAmount - proxyQuota;

                Assert.True(finalMainFuelBalance == (startingMainFuelBalance + leftover - txCost));
                Assert.True(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota));

                unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, NativeContractKind.Stake, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
                Assert.True(unclaimedAmount == 0);
            }*/
    
    

    [Fact]
    public void MapClearBug()
    {
        var scriptString = new string[]
        {
            $"@clearMe: nop",

            $"load r0 \"world\"",
            $"push r0",
            $"load r0 \"hello\"",
            $"push r0",
            $"load r0 \"dummy\"",
            $"push r0",
            $"load r0 \"Map.Set\"",
            $"extcall r0",

            $"load r0 \"dummy\"",
            $"push r0",
            $"load r0 \"Map.Clear\"",
            $"extcall r0",
            $"ret"
        };


        Dictionary<string, int> labels;
        DebugInfo debugInfo;
        var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

        var methods = new ContractMethod[] { new ContractMethod("clearMe", VMType.None, 0, new ContractParameter[0]) };
        var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());

        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
            ScriptUtils.BeginScript().AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 999999)
                .CallInterop("Runtime.DeployContract", owner.Address, "test", script, abi.ToByteArray())
                .SpendGas(owner.Address).EndScript());
        simulator.EndBlock().First();

        simulator.BeginBlock();
        var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 9999)
                .CallContract("test", "clearMe")
                .SpendGas(owner.Address).EndScript());
        var block = simulator.EndBlock().First();
    }

}


