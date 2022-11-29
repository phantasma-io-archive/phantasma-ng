using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Types;
using Phantasma.Simulator;

namespace Phantasma.LegacyTests.ContractTests;

[TestClass]
public class AccountContracTest
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

    [TestInitialize]
    public void Initialize()
    {
        sysAddress = SmartContract.GetAddressForNative(NativeContractKind.Friends);
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
        SetInitialBalance(user.Address);
    }

    protected void SetInitialBalance(Address address)
    {
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.FuelTokenSymbol, initialFuel);
        simulator.GenerateTransfer(owner, address, nexus.RootChain, DomainSettings.StakingTokenSymbol, initialAmount);
        simulator.EndBlock();
        Assert.IsTrue(simulator.LastBlockWasSuccessful());
    }
    

    [TestMethod]
    public void AccountTriggersAllowance()
    {
        string[] scriptString;
        
        var target = PhantasmaKeys.Generate();
        var other = PhantasmaKeys.Generate();

        var symbol = "SOUL";
        var addressStr = Base16.Encode(other.Address.ToByteArray());

        scriptString = new string[]
        {
            $"alias r1, $temp",
            $"alias r2, $from",
            $"alias r3, $to",
            $"alias r4, $symbol",
            $"alias r5, $amount",

            $"jmp @end",

            $"@OnReceive: nop",
            $"pop $from",
            $"pop $to",
            $"pop $symbol",
            $"pop $amount",

            $@"load r1 ""{symbol}""",
            $@"equal r1, $symbol, $temp",
            $"jmpnot $temp, @end",

            $"load $temp 2",
            $"div $amount $temp $temp",

            $"push $temp",
            $"push $symbol",
            $"load r11 0x{addressStr}",
            $"push r11",
            $@"extcall ""Address()""",
            $"push $to",

            $"load r0 \"Runtime.TransferTokens\"",
            $"extcall r0",
            $"jmp @end",

            $"@end: ret"
        };


        Dictionary<string, int> labels;
        DebugInfo debugInfo;
        var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

        var methods = TokenUtils.GetTriggersForABI(labels);
        var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "KCAL", UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals));
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals));
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(target, ProofOfWork.None,
            () => ScriptUtils.BeginScript()
                    .AllowGas(target.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), target.Address, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals))
                    .SpendGas(target.Address)
                .EndScript());
        simulator.EndBlock();


        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(target, ProofOfWork.None,
            () => ScriptUtils.BeginScript()
                    .AllowGas(target.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterScript), target.Address, script, abi.ToByteArray())
                    .SpendGas(target.Address)
                .EndScript());
        simulator.EndBlock();

        var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, target.Address);

        var amount = UnitConversion.ToBigInteger(5, DomainSettings.StakingTokenDecimals);

        simulator.BeginBlock();
        var tx = simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, symbol, amount * 2);
        simulator.EndBlock();

        var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, target.Address);
        var expectedBalance = initialBalance + amount;
        Assert.IsTrue(balance == expectedBalance);

        balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, other.Address);
        Assert.IsTrue(balance == amount);
    }

    [TestMethod]
    [Ignore]
    public void AccountTriggersEventPropagation()
    {
        string[] scriptString;

        var owner = PhantasmaKeys.Generate();
        var target = PhantasmaKeys.Generate();
        var symbol = "TEST";
        var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        string message = "customEvent";
        var addressStr = Base16.Encode(target.Address.ToByteArray());
        var isTrue = true;

        scriptString = new string[]
        {
            $"alias r1, $triggerSend",
            $"alias r2, $triggerReceive",
            $"alias r3, $triggerBurn",
            $"alias r4, $triggerMint",
            $"alias r5, $currentTrigger",
            $"alias r6, $comparisonResult",
            $"alias r7, $triggerWitness",
            $"alias r8, $currentAddress",
            $"alias r9, $sourceAddress",

            $@"load $triggerSend, ""{AccountTrigger.OnSend}""",
            $@"load $triggerReceive, ""{AccountTrigger.OnReceive}""",
            $@"load $triggerBurn, ""{AccountTrigger.OnBurn}""",
            $@"load $triggerMint, ""{AccountTrigger.OnMint}""",
            $@"load $triggerWitness, ""{AccountTrigger.OnWitness}""",
            $"pop $currentTrigger",
            $"pop $currentAddress",

            $"equal $triggerWitness, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @witnessHandler",

            $"equal $triggerSend, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @sendHandler",

            $"equal $triggerReceive, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @receiveHandler",

            $"equal $triggerBurn, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @burnHandler",

            $"equal $triggerMint, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @OnMint",

            $"jmp @end",

            $"@witnessHandler: ",
            $"load r11 0x{addressStr}",
            $"push r11",
            "extcall \"Address()\"",
            $"pop $sourceAddress",
            $"equal $sourceAddress, $currentAddress, $comparisonResult",
            "jmpif $comparisonResult, @endWitness",
            $"load r1 \"test witness handler xception\"",
            $"throw r1",
            
            "jmp @end",

            $"@sendHandler: jmp @end",

            $"@receiveHandler: jmp @end",

            $"@burnHandler: jmp @end",

            $"@OnMint: load r11 0x{addressStr}",
            $"push r11",
            $@"extcall ""Address()""",
            $"pop r11",

            $"load r10, {(int)EventKind.Custom}",
            $@"load r12, ""{message}""",

            $"push r12",
            $"push r11",
            $"push r10",
            $@"extcall ""Runtime.Notify""",

            $"@endWitness: ret",
            $"load r11 {isTrue}",
            $"push r11",

            $"@end: ret"
        };

        var script = AssemblerUtils.BuildScript(scriptString, null, out var something, out var labels);
        var methods = new List<ContractMethod>();
        methods.Add(new ContractMethod("OnMint", VMType.None, 205, new ContractParameter[0]));
        var abi = new ContractInterface(methods, new List<ContractEvent>());

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "KCAL", 60000000000000);
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)*50000);
        simulator.GenerateCustomTransaction(target, ProofOfWork.None,
            () => ScriptUtils.BeginScript().AllowGas(target.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit)
                .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), target.Address, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)*50000)
                .CallContract(NativeContractKind.Account, nameof(AccountContract.RegisterScript), target.Address, script, abi.ToByteArray()).SpendGas(target.Address)
                .EndScript());
        simulator.EndBlock();

        simulator.BeginBlock();
        simulator.GenerateToken(target, symbol, $"{symbol}Token", 1000000000, 3, flags);
        var tx = simulator.MintTokens(target, target.Address, symbol, 1000);
        simulator.EndBlock();

        var accountScript = simulator.Nexus.LookUpAddressScript(simulator.Nexus.RootStorage, target.Address, simulator.CurrentTime);
        Assert.IsTrue(accountScript != null && accountScript.Length > 0);

        var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);
        var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, target.Address);
        Assert.IsTrue(balance == 1000);

        var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
        Assert.IsTrue(events.Count(x => x.Kind == EventKind.Custom) == 1);

        var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
        var eventMessage = (VMObject)Serialization.Unserialize(eventData, typeof(VMObject));

        Assert.IsTrue(eventMessage.AsString() == message);
        Assert.ThrowsException<ChainException>(() =>
        {
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, symbol, 10);
            simulator.EndBlock();
        });

        balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, target.Address);
        Assert.IsTrue(balance == 1000);
    }
    
            [TestMethod]
    public void AccountRegister()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var fuelSymbol = DomainSettings.FuelTokenSymbol;

        Func<PhantasmaKeys, string, bool> registerName = (keypair, name) =>
        {
            bool result = true;

            try
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateAccountRegistration(keypair, name);
                var lastBlock = simulator.EndBlock().FirstOrDefault();

                if (lastBlock != null)
                {
                    Assert.IsTrue(tx != null);

                    var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                    Assert.IsTrue(evts.Any(x => x.Kind == EventKind.AddressRegister));
                }
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        };

        var testUser = PhantasmaKeys.Generate();

        var token = nexus.GetTokenInfo(nexus.RootStorage, fuelSymbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);

        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);

        var initialBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.StakingTokenSymbol, owner.Address);
        Assert.IsTrue(initialBalance >= stakeAmount);

        // Send from Genesis address to test user
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, fuelSymbol, amount);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();

        // verify test user balance
        var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.IsTrue(balance == amount);

        // make user stake enough to register a name
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit).
                CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeAmount).
                SpendGas(testUser.Address).
                EndScript());
        simulator.EndBlock();

        var targetName = "hello";
        Assert.IsFalse(registerName(testUser, targetName.Substring(3)));
        Assert.IsFalse(registerName(testUser, targetName.ToUpper()));
        Assert.IsFalse(registerName(testUser, targetName + "!"));
        Assert.IsTrue(registerName(testUser, targetName));

        var currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address, simulator.CurrentTime);
        Assert.IsTrue(currentName == targetName);

        var someAddress = nexus.LookUpName(nexus.RootStorage, targetName, simulator.CurrentTime);
        Assert.IsTrue(someAddress == testUser.Address);

        Assert.IsFalse(registerName(testUser, "other"));
    }

    [TestMethod]
    public void AccountMigrate()
    {
        var symbol = DomainSettings.FuelTokenSymbol;

        var testUser = PhantasmaKeys.Generate();

        var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
        var amount = UnitConversion.ToBigInteger(100, token.Decimals);

        var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);

        // Send from Genesis address to test user
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
        simulator.EndBlock();

        // verify test user balance
        var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
        Assert.IsTrue(balance == amount);

        // make user stake enough to register a name
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), testUser.Address, stakeAmount).
                SpendGas(testUser.Address).
                EndScript());
        simulator.EndBlock();

        var targetName = "hello";

        simulator.BeginBlock();
        var tx = simulator.GenerateAccountRegistration(testUser, targetName);
        var lastBlock = simulator.EndBlock().FirstOrDefault();

        if (lastBlock != null)
        {
            Assert.IsTrue(tx != null);

            var evts = lastBlock.GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(evts.Any(x => x.Kind == EventKind.AddressRegister));
        }


        var currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address, simulator.CurrentTime);
        Assert.IsTrue(currentName == targetName);

        var someAddress = nexus.LookUpName(nexus.RootStorage, targetName, simulator.CurrentTime);
        Assert.IsTrue(someAddress == testUser.Address);

        var migratedUser = PhantasmaKeys.Generate();

        simulator.BeginBlock();
        tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
        {
            return ScriptUtils.BeginScript().
                 AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, simulator.MinimumGasLimit).
                 CallContract(NativeContractKind.Account, nameof(AccountContract.Migrate), testUser.Address, migratedUser.Address).
                 SpendGas(testUser.Address).
                 EndScript();
        });
        simulator.EndBlock().FirstOrDefault();

        currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address, simulator.CurrentTime);
        Assert.IsFalse(currentName == targetName);

        var newName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, migratedUser.Address, simulator.CurrentTime);
        Assert.IsTrue(newName == targetName);
    }
}
