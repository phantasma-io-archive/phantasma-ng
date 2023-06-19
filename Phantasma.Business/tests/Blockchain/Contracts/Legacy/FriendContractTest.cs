using System;
using System.Numerics;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.Tests.Simulator;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Enums;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;

using Xunit;

namespace Phantasma.Business.Tests.Blockchain.Contracts.Legacy;

[Collection(nameof(SystemTestCollectionDefinition))]
public class FriendContractTest
{
    public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

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

    public FriendContractTest()
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
        initialAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);
        initialFuel = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
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
    public void TestFriendsContract()
    {
        var stakeAmount = MinimumValidStake;
        double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
        double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

        var fuelToken = DomainSettings.FuelTokenSymbol;
        var stakingToken = DomainSettings.StakingTokenSymbol;

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(user.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), user.Address, stakeAmount)
                .SpendGas(user.Address)
                .EndScript());
        simulator.EndBlock();

        var unclaimedAmount = simulator.InvokeContract(NativeContractKind.Stake, nameof(StakeContract.GetUnclaimed), user.Address).AsNumber();
        double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

        Assert.True(realUnclaimedAmount == realExpectedUnclaimedAmount);

        BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
        Assert.True(actualEnergyRatio == DefaultEnergyRatioDivisor);
    }

    private struct FriendTestStruct
    {
        public string name;
        public Address address;
    }

    private byte[] GetScriptForFriends(Address target)
    {
        var fuelToken = DomainSettings.FuelTokenSymbol;
        var stakingToken = DomainSettings.StakingTokenSymbol;

        //Let A be an address
        var testUserA = PhantasmaKeys.Generate();
        var testUserB = PhantasmaKeys.Generate();
        var testUserC = PhantasmaKeys.Generate();

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, fuelToken, initialFuel);
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, stakingToken, 100000000);
        simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, fuelToken, initialFuel);
        simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, stakingToken, 100000000);
        simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, fuelToken, initialFuel);
        simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, stakingToken, 100000000);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                .CallContract(NativeContractKind.Friends, nameof(FriendsContract.AddFriend), testUserA.Address, testUserB.Address)
                .SpendGas(testUserA.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Friends, nameof(FriendsContract.AddFriend), testUserA.Address, testUserC.Address)
                .SpendGas(testUserA.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

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

    [Fact(Skip = "Ignore test array")]
    public void TestFriendArray()
    {
        var fuelToken = DomainSettings.FuelTokenSymbol;
        var stakingToken = DomainSettings.StakingTokenSymbol;

        //Let A be an address
        var testUserA = PhantasmaKeys.Generate();
        var testUserB = PhantasmaKeys.Generate();
        var testUserC = PhantasmaKeys.Generate();

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, fuelToken, initialFuel);
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, stakingToken, 100000000);
        simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, fuelToken, initialFuel);
        simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, stakingToken, 100000000);
        simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, fuelToken, initialFuel);
        simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, stakingToken, 100000000);
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Friends, nameof(FriendsContract.AddFriend), testUserA.Address, testUserB.Address)
                .SpendGas(testUserA.Address)
                .EndScript());
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            ScriptUtils.BeginScript()
                .AllowGas(testUserA.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Friends, nameof(FriendsContract.AddFriend), testUserA.Address, testUserC.Address)
                .SpendGas(testUserA.Address)
                .EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var scriptA = GetScriptForFriends(testUserA.Address);
        var resultA = simulator.InvokeScript(scriptA);
        Assert.True(resultA != null);

        var tempA = resultA.ToArray<FriendTestStruct>();
        Assert.True(tempA.Length == 2);
        Assert.True(tempA[0].address == testUserB.Address);
        Assert.True(tempA[1].address == testUserC.Address);

        /*
        // we also test that the API can handle complex return types
        var api = new NexusAPI(nexus);
        var apiResult = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(scriptA));

        // NOTE objBytes will contain a serialized VMObject
        var objBytes = Base16.Decode(apiResult.results[0]);
        var resultB = Serialization.Unserialize<VMObject>(objBytes);

        // finally as last step, convert it to a C# struct
        var tempB = resultB.ToArray<FriendTestStruct>();
        Assert.True(tempB.Length == 2);
        Assert.True(tempB[0].address == testUserB.Address);
        Assert.True(tempB[1].address == testUserC.Address);

        // check what happens when no friends available
        var scriptB = GetScriptForFriends(testUserB.Address);
        var apiResultB = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(scriptB));

        // NOTE objBytes will contain a serialized VMObject
        var objBytesB = Base16.Decode(apiResultB.results[0]);
        var resultEmpty = Serialization.Unserialize<VMObject>(objBytesB);
        Assert.True(resultEmpty != null);*/
    }
}
