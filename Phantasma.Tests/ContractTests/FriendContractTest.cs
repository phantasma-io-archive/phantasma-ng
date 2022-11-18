using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Simulator;

namespace Phantasma.LegacyTests.ContractTests;

[TestClass]
public class FriendContractTest
{
    public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

    public uint DefaultEnergyRatioDivisor => StakeContract.DefaultEnergyRatioDivisor;

    public BigInteger StakeToFuel(BigInteger stakeAmount, uint _currentEnergyRatioDivisor)
    {
        return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _currentEnergyRatioDivisor;
    }
    
        [TestMethod]
        public void TestFriendsContract()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var stakeAmount = MinimumValidStake;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            var fuelToken = DomainSettings.FuelTokenSymbol;
            var stakingToken = DomainSettings.StakingTokenSymbol;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, stakingToken, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, 9999)
                    .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, NativeContractKind.Stake, "GetUnclaimed", testUser.Address).AsNumber();
            double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

            BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
            Assert.IsTrue(actualEnergyRatio == DefaultEnergyRatioDivisor);
        }

        private struct FriendTestStruct
        {
            public string name;
            public Address address;
        }

        private byte[] GetScriptForFriends(Address target)
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var fuelToken = DomainSettings.FuelTokenSymbol;
            var stakingToken = DomainSettings.StakingTokenSymbol;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();
            var testUserC = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserB.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserC.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var scriptString = new string[]
            {
                "load r0 \"friends\"",
                "ctx r0 r1",

                $"load r0 0x{Base16.Encode(target.ToByteArray())}",
                "push r0",
                "extcall \"Address()\"",

                "load r0 \"GetFriends\"",
                "push r0",
                "switch r1",

                "alias r4 $friends",
                "alias r5 $address",
                "alias r6 $name",
                "alias r7 $i",
                "alias r8 $count",
                "alias r9 $loopflag",
                "alias r10 $friendname",
                "alias r11 $friendnamelist",

                "pop r0",
                "cast r0 $friends #Struct",
                "count $friends $count",

                "load $i 0",
                "@loop: ",
                "lt $i $count $loopflag",
                "jmpnot $loopflag @finish",

                "get $friends $address $i",
                "push $address",
                "call @lookup",
                "pop $name",

                "load r0 \"name\"",
                "load r1 \"address\"",
                "put $name $friendname[r0]",
                "put $address $friendname[r1]",

                "put $friendname $friendnamelist $i",

                "inc $i",
                "jmp @loop",
                "@finish: push $friendnamelist",
                "ret",

                "@lookup: load r0 \"account\"",
                "ctx r0 r1",
                "load r0 \"LookUpAddress\"",
                "push r0",
                "switch r1",
                "ret"
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            return script;
        }

        [TestMethod]
        public void TestFriendArray()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner);
            var nexus = simulator.Nexus;

            var fuelToken = DomainSettings.FuelTokenSymbol;
            var stakingToken = DomainSettings.StakingTokenSymbol;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();
            var testUserC = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserB.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserC.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var scriptA = GetScriptForFriends(testUserA.Address);
            var resultA = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptA);
            Assert.IsTrue(resultA != null);

            var tempA = resultA.ToArray<FriendTestStruct>();
            Assert.IsTrue(tempA.Length == 2);
            Assert.IsTrue(tempA[0].address == testUserB.Address);
            Assert.IsTrue(tempA[1].address == testUserC.Address);

            /*
            // we also test that the API can handle complex return types
            var api = new NexusAPI(nexus);
            var apiResult = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(scriptA));

            // NOTE objBytes will contain a serialized VMObject
            var objBytes = Base16.Decode(apiResult.results[0]);
            var resultB = Serialization.Unserialize<VMObject>(objBytes);

            // finally as last step, convert it to a C# struct
            var tempB = resultB.ToArray<FriendTestStruct>();
            Assert.IsTrue(tempB.Length == 2);
            Assert.IsTrue(tempB[0].address == testUserB.Address);
            Assert.IsTrue(tempB[1].address == testUserC.Address);

            // check what happens when no friends available
            var scriptB = GetScriptForFriends(testUserB.Address);
            var apiResultB = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(scriptB));

            // NOTE objBytes will contain a serialized VMObject
            var objBytesB = Base16.Decode(apiResultB.results[0]);
            var resultEmpty = Serialization.Unserialize<VMObject>(objBytesB);
            Assert.IsTrue(resultEmpty != null);*/
        }

}
