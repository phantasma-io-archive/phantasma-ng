using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.Token.Structs;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;

namespace Phantasma.Business.Tests.Blockchain;

using Xunit;
using Phantasma.Business.Blockchain.VM;

public class DescriptionVMTests
{
    // create a class based on the DescriptionVM abstract class
    public class TestVM : DescriptionVM
    {
        public string Symbol;
        public Address Address;
        public string SymbolToken;
        public IToken token;

        
        public TestVM(byte[] script, uint offset) : base(script, offset)
        {
        }

        public override IToken FetchToken(string symbol)
        {
            SymbolToken = symbol;
            var flags = TokenFlags.Burnable | TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Swappable | TokenFlags.Finite | TokenFlags.Divisible;
            var script = new byte[32]; 
            token = new TokenInfo("SOUL", "Phantasma SOUL", Address.Null, 1000, 8, flags , script, null);
            return token;
        }

        public override string OutputAddress(Address address)
        {
            Address = address;
            return "null";
        }

        public override string OutputSymbol(string symbol)
        {
            Symbol= symbol;
            return symbol;
        }
    }
    
    [Fact]
    public void TestRegisterMethod()
    {
        // create a new description VM
        var script = new byte[512];
        uint offset = 0;
        var vm = new TestVM(script, offset);
        bool enteredTheTest = false;
        // test the description VM
        vm.RegisterMethod("testMethod", (vm) =>
        {
            vm.PushFrame(vm.CurrentContext, 0, 1);
            vm.PushFrame(vm.CurrentContext, 0, 1);
            enteredTheTest = true;
            return ExecutionState.Halt;
        });
        
        // test the method
        vm.ExecuteInterop("testMethod");
        
        Assert.True(enteredTheTest);
    }
    
    [Fact]
    public void TestDumpData()
    {
        // create a new description VM
        var script = new byte[32];
        uint offset = 0;
        var vm = new TestVM(script, offset);
        
        // test the description VM
        List<string> lines = new List<string>();
        vm.DumpData(lines);
        
        Assert.NotNull(vm);
    }

    private byte[] MakeScript(PhantasmaKeys keys)
    {
        var sb = ScriptUtils.BeginScript();
        
        // Add the first call
        byte symbol_reg = 0;
        sb.EmitLoad(symbol_reg, "SOUL");
        sb.EmitPush(symbol_reg);
        
        byte address_reg = 1;
        sb.EmitLoad(address_reg, keys.Address);
        sb.EmitPush(address_reg);

        address_reg++; // 2
        sb.EmitLoad(address_reg, keys.Address.Text);
        sb.EmitPush(address_reg);

        /*address_reg++; // 3
        sb.EmitLoad(address_reg, keys.Address.ToByteArray());
        sb.EmitPush(address_reg);*/
        
        byte decimals_reg = 4;
        sb.EmitLoad(decimals_reg, new BigInteger((int)10));
        sb.EmitPush(decimals_reg);

        decimals_reg++; // 5
        sb.EmitLoad(decimals_reg, "SOUL");
        sb.EmitPush(decimals_reg);

        byte src_reg = 0;
        byte dest_reg = 1;
        sb.Emit(Opcode.CTX, new byte[] { src_reg, dest_reg });

        sb.Emit(Opcode.SWITCH, new byte[] { dest_reg });
        return sb.EndScript();
    }
    
    [Fact]
    public void TestExecuteInterop()
    {
        // create a new description VM
        var keys = PhantasmaKeys.Generate();
        var script = MakeScript(keys);
        uint offset = 0;
        var vm = new TestVM(script, offset);
        
        // test Interop for Symbol
        var testSymbol = "Format." + "Symbol";
        vm.Stack.Push(VMObject.FromObject("SOUL"));
        vm.ExecuteInterop(testSymbol);
        
        Assert.Equal("SOUL", vm.Symbol);
        
        // test Interop for Address
        var testAddress = "Format." + "Account";
        vm.Stack.Push(VMObject.FromObject(keys.Address));
        vm.ExecuteInterop(testAddress);
        Assert.Equal(keys.Address.Text, vm.Address.Text);
        
        vm.Stack.Push(VMObject.FromObject(keys.Address.Text));
        vm.ExecuteInterop(testAddress);
        Assert.Equal(keys.Address.Text, vm.Address.Text);
        
        /*vm.Stack.Push(VMObject.FromBytes(keys.Address.ToByteArray()));
        vm.ExecuteInterop(testAddress);
        Assert.Equal(keys.Address.Text, vm.Address.Text);*/

        // test interop for Decimals
        var testDecimals = "Format." + "Decimals";
        vm.Stack.Push(VMObject.FromObject("SOUL"));
        vm.Stack.Push(VMObject.FromObject(new BigInteger(10)));
        vm.ExecuteInterop(testDecimals);
        
        Assert.Equal("SOUL", vm.token.Symbol);
    }
    
    [Fact]
    public void TestLoadContext()
    {
        // create a new description VM
        var script = new byte[32];
        uint offset = 0;
        var vm = new TestVM(script, offset);
        
        // test the description VM
        Assert.Throws<NotImplementedException>(() => vm.LoadContext("ctx"));
    }
}
