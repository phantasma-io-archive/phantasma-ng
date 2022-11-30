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

[Collection("TokenTests")]
public class TokenTests
{
    [Fact]
    public void FuelTokenTransfer()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var accountChain = nexus.GetChainByName("account");
        var symbol = DomainSettings.FuelTokenSymbol;
        var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

        var testUserA = PhantasmaKeys.Generate();
        var testUserB = PhantasmaKeys.Generate();

        var amount = UnitConversion.ToBigInteger(2, token.Decimals);

        // Send from Genesis address to test user A
        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain as Chain, symbol, amount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserA.Address);

        Assert.True(oldBalance == amount);

        // Send from test user A address to test user B
        amount /= 2;
        simulator.BeginBlock();
        var tx = simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain as Chain, symbol, amount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // verify test user balance
        var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);
        Assert.True(transferBalance == amount);

        var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserA.Address);
        var gasFee = nexus.RootChain.GetTransactionFee(tx);

        var expectedFee = oldBalance - (newBalance + transferBalance);
        Assert.True(expectedFee == gasFee);

        var sum = transferBalance + newBalance + gasFee;
        Assert.True(sum == oldBalance);
    }

    [Fact]
    public void TokenTriggers()
    {
        string[] scriptString;
        //TestVM vm;


        var owner = PhantasmaKeys.Generate();
        var target = PhantasmaKeys.Generate();
        var symbol = "TEST";
        var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        /*string message = "customEvent";
        var addressStr = Base16.Encode(owner.Address.ToByteArray());

        scriptString = new string[]
        {
            $"alias r1, $triggerSend",
            $"alias r2, $triggerReceive",
            $"alias r3, $triggerBurn",
            $"alias r4, $triggerMint",
            $"alias r5, $currentTrigger",
            $"alias r6, $comparisonResult",

            $@"load $triggerSend, ""{TokenTrigger.OnSend}""",
            $@"load $triggerReceive, ""{TokenTrigger.OnReceive}""",
            $@"load $triggerBurn, ""{TokenTrigger.OnBurn}""",
            $@"load $triggerMint, ""{TokenTrigger.OnMint}""",
            $"pop $currentTrigger",

            $"equal $triggerSend, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @sendHandler",

            $"equal $triggerReceive, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @receiveHandler",

            $"equal $triggerBurn, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @burnHandler",

            $"equal $triggerMint, $currentTrigger, $comparisonResult",
            $"jmpif $comparisonResult, @OnMint",

            $"ret",

            $"@sendHandler: ret",

            $"@receiveHandler: ret",

            $"@burnHandler: load r7 \"test burn handler exception\"",
            $"throw r7",

            $"@OnMint: ret",
        };

        var script = AssemblerUtils.BuildScript(scriptString, null, out var debugInfo, out var labels);
        */

        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, $"{symbol}Token", 1000000000, 3, flags/*, script, labels*/);
        var tx = simulator.MintTokens(owner, owner.Address, symbol, 1000);
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain as Chain, symbol, 10);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);
        var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
        Assert.True(balance == 990, $"{balance} == 990");

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, -10);
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
        Assert.True(balance+10 == 1000, $"{balance} == 1000");
    }

    [Fact]
    public void TokenTriggersEventPropagation()
    {
        string[] scriptString;
        //TestVM vm;


        var owner = PhantasmaKeys.Generate();
        var target = PhantasmaKeys.Generate();
        var symbol = "TEST";
        var decimals = 10;
        var totalSupply = 1000000000;
        var name = $"{symbol}Token";
        var flags = TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Fungible | TokenFlags.Divisible;

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        string message = "customEvent";
        var addressStr = Base16.Encode(owner.Address.ToByteArray());

        scriptString = new string[]
        {
                    $"alias r3, $result",
                    $"alias r4, $owner",

                    $"@getOwner: nop",
                    $"load $owner 0x{addressStr}",
                    "push $owner",
                    $"jmp @end",

                    $"@getSymbol: nop",
                    $"load r0 \""+symbol+"\"",
                    "push r0",
                    $"jmp @end",
           
                    $"@getName: nop",
                    $"load r0 \""+name+"\"",
                    "push r0",
                    $"jmp @end",

                    $"@getMaxSupply: nop",
                    $"load r0 "+totalSupply+"",
                    "push r0",
                    $"jmp @end",

                    $"@getDecimals: nop",
                    $"load r0 "+decimals+"",
                    "push r0",
                    $"jmp @end",

                    $"@getTokenFlags: nop",
                    $"load r0 "+(int)flags+"",
                    "push r0",
                    $"jmp @end",

            $"@{TokenTrigger.OnMint}: load r11 0x{addressStr}",
            $"push r11",
            $@"extcall ""Address()""",
            $"pop r11",

            $"load r10, {(int)EventKind.Custom}",
            $@"load r12, ""{message}""",

            $"push r12",
            $"push r11",
            $"push r10",
            $@"extcall ""Runtime.Notify""",
            "ret",

            $"@return: ret",
                    $"@end: ret"
        };

        var script = AssemblerUtils.BuildScript(scriptString, null, out var debugInfo, out var labels);

        simulator.BeginBlock();
        simulator.GenerateToken(owner, symbol, name, totalSupply, decimals, flags, script, labels);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        simulator.BeginBlock();
        var tx = simulator.MintTokens(owner, owner.Address, symbol, 1000);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var events = simulator.Nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);
        Assert.True(events.Count(x => x.Kind == EventKind.Custom) == 1, $"{events.Count(x => x.Kind == EventKind.Custom)} == 1");

        var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);
        var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
        Assert.True(balance == 1000);

        var eventData = events.First(x => x.Kind == EventKind.Custom).Data;
        var eventMessage = (VMObject)Serialization.Unserialize(eventData, typeof(VMObject));

        Assert.True(eventMessage.AsString() == message);

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, symbol, 10000);
        simulator.EndBlock();
        Assert.False(simulator.LastBlockWasSuccessful());

        balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);
        Assert.True(balance == 1000);
    }

    [Fact]
    public void TransferToAccountName()
    {
        var owner = PhantasmaKeys.Generate();

        var simulator = new NexusSimulator(owner);
        var nexus = simulator.Nexus;

        var symbol = DomainSettings.FuelTokenSymbol;

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
                    Assert.True(tx != null);

                    var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                    Assert.True(evts.Any(x => x.Kind == EventKind.AddressRegister));
                }
            }
            catch (Exception)
            {
                result = false;
            }

            return result;
        };

        var targetName = "hello";
        var testUser = PhantasmaKeys.Generate();
        var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
        var amount = UnitConversion.ToBigInteger(10, token.Decimals);

        simulator.BeginBlock();
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
        simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, amount);
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        // NOTE we split this into blocks, however if you put all txs in a single block it fails
        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                .CallContract(NativeContractKind.Stake, "Stake", testUser.Address, amount).
                SpendGas(testUser.Address).EndScript());
        simulator.EndBlock();

        Assert.True(registerName(testUser, targetName));

        // Send from Genesis address to test user
        var transferAmount = 1;

        var initialFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

        simulator.BeginBlock();
        simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().AllowGas(owner.Address, Address.Null, simulator.MinimumFee, Transaction.DefaultGasLimit)
                .TransferTokens(token.Symbol, owner.Address, targetName, transferAmount)
                .SpendGas(owner.Address).EndScript());
        simulator.EndBlock();
        Assert.True(simulator.LastBlockWasSuccessful());

        var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

        Assert.True(finalFuelBalance - initialFuelBalance == transferAmount);
    }

}
