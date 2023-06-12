using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.VM;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.VM;

[Collection(nameof(SystemTestCollectionDefinition))]
public class DisassemblerTest
{
    [Fact]
    public void GetInstructions_should_return_list_of_instructions_for_contract_call()
    {
        // Arrange
        var sut = new Disassembler(ScriptContextConstants.CustomContractScript);

        // Act
        var result = sut.Instructions.ToArray();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.Length.ShouldBe(76);
        result.First().Opcode.ShouldBe(Opcode.LOAD);
        result.Last().Opcode.ShouldBe(Opcode.RET);
    }

    [Fact]
    public void GetInstructions_should_return_list_of_instructions_for_nft_transfer()
    {
        // Arrange
        var sut = new Disassembler(ScriptContextConstants.TransferNftScript);

        // Act
        var result = sut.Instructions.ToArray();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.Length.ShouldBe(31);
        result.First().Opcode.ShouldBe(Opcode.LOAD);
        result.Last().Opcode.ShouldBe(Opcode.RET);
    }

    [Theory]
    [InlineData(new byte[] { (int)Opcode.NOP }, "000: NOP")]
    [InlineData(new byte[] { (int)Opcode.MOVE, 1, 2 }, "000: MOVE r1, r2")]
    [InlineData(new byte[] { (int)Opcode.COPY, 1, 2 }, "000: COPY r1, r2")]
    [InlineData(new byte[] { (int)Opcode.PUSH, 1 }, "000: PUSH r1")]
    [InlineData(new byte[] { (int)Opcode.POP, 1 }, "000: POP r1")]
    [InlineData(new byte[] { (int)Opcode.SWAP, 1, 2 }, "000: SWAP r1, r2")]
    [InlineData(new byte[] { (int)Opcode.CALL, 1, 1, 1 }, "000: CALL r1, 257")] // Needs validation
    [InlineData(new byte[] { (int)Opcode.EXTCALL, 1 }, "000: EXTCALL r1")]
    [InlineData(new byte[] { (int)Opcode.JMP, 1, 2 }, "000: JMP")]
    //[InlineData(new byte[] { (int)Opcode.JMPIF }, "000: JMPIF r1, @label")]
    //[InlineData(new byte[] { (int)Opcode.JMPNOT }, "000: JMPNOT r1, @label")]
    [InlineData(new byte[] { (int)Opcode.RET }, "000: RET")]
    [InlineData(new byte[] { (int)Opcode.THROW, 1 }, "000: THROW r1")]
    [InlineData(new byte[] { (int)Opcode.CAST, 1, 2, 3 }, "000: CAST r1, r2, 3")]
    [InlineData(new byte[] { (int)Opcode.CAT, 1, 2, 3 }, "000: CAT r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.RANGE, 1, 2, 0, 1 }, "000: RANGE r1, r2, 0, 1")]
    [InlineData(new byte[] { (int)Opcode.LEFT, 1, 2, 5 }, "000: LEFT r1, r2, 5")]
    [InlineData(new byte[] { (int)Opcode.RIGHT, 1, 2, 5 }, "000: RIGHT r1, r2, 5")]
    [InlineData(new byte[] { (int)Opcode.SIZE, 1, 2 }, "000: SIZE r1, r2")]
    [InlineData(new byte[] { (int)Opcode.COUNT, 1, 2 }, "000: COUNT r1, r2")]
    [InlineData(new byte[] { (int)Opcode.NOT, 1, 1 }, "000: NOT r1, r1")]
    [InlineData(new byte[] { (int)Opcode.AND, 1, 2, 3 }, "000: AND r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.OR, 1, 2, 3 }, "000: OR r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.XOR, 1, 2, 3 }, "000: XOR r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.EQUAL, 1, 2, 3 }, "000: EQUAL r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.LT, 1, 2, 3 }, "000: LT r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.GT, 1, 2, 3 }, "000: GT r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.LTE, 1, 2, 3 }, "000: LTE r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.GTE, 1, 2, 3 }, "000: GTE r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.INC, 1 }, "000: INC r1")]
    [InlineData(new byte[] { (int)Opcode.DEC, 1 }, "000: DEC r1")]
    [InlineData(new byte[] { (int)Opcode.SIGN, 1, 2 }, "000: SIGN r1, r2")]
    [InlineData(new byte[] { (int)Opcode.NEGATE, 1, 2 }, "000: NEGATE r1, r2")]
    [InlineData(new byte[] { (int)Opcode.ABS, 1, 2 }, "000: ABS r1, r2")]
    [InlineData(new byte[] { (int)Opcode.ADD, 1, 2, 3 }, "000: ADD r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.SUB, 1, 2, 3 }, "000: SUB r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.MUL, 1, 2, 3 }, "000: MUL r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.DIV, 1, 2, 3 }, "000: DIV r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.MOD, 1, 2, 3 }, "000: MOD r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.SHL, 1, 2, 3 }, "000: SHL r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.SHR, 1, 2, 3 }, "000: SHR r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.MIN, 1, 2, 3 }, "000: MIN r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.MAX, 1, 2, 3 }, "000: MAX r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.POW, 1, 2, 3 }, "000: POW r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.CTX, 1, 2 }, "000: CTX r1, r2")]
    [InlineData(new byte[] { (int)Opcode.SWITCH, 1 }, "000: SWITCH r1")]
    [InlineData(new byte[] { (int)Opcode.PUT, 1, 2, 3 }, "000: PUT r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.GET, 1, 2, 3 }, "000: GET r1, r2, r3")]
    [InlineData(new byte[] { (int)Opcode.CLEAR, 1 }, "000: CLEAR r1")]
    [InlineData(new byte[] { (int)Opcode.UNPACK, 1, 2 }, "000: UNPACK r1, r2")]
    //[InlineData(new byte[] { (int)Opcode.PACK, 1, 2 }, "000: PACK r1, r2")] // Not implemented in ScriptContext
    //[InlineData(new byte[] { (int)Opcode.SUBSTR, 33 }, "000: SUBSTR r33")] // Not implemented in ScriptContext
    public void ToString_should_return_expected_value(byte[] script, string expected)
    {
        // Arrange
        var sut = new Disassembler(script);

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(expected);
    }

    [Fact]
    public void ToString_should_return_expected_value_for_load()
    {
        // Arrange
        var sut = new Disassembler(ScriptUtils.BeginScript().EmitLoad(1, new byte[] { 112, 23 }, VMType.Number)
            .EmitPush(1).EndScript());

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe($"000: LOAD r1, 6000{Environment.NewLine}006: PUSH r1{Environment.NewLine}008: RET");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2048)]
    public void ReadVar_internal_should_read_bytes(int length)
    {
        // Arrange
        var sut = new Disassembler(ScriptUtils.BeginScript().EmitLoad(1, new byte[length]).EmitPush(1).EndScript());

        // Act
        var result = Should.NotThrow(() => sut.ToString());

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Test_JMP()
    {
        // create a var bytes for the string Test()
        //sb.EmitLoad(src_reg, contractName);
        var script = ScriptUtils.BeginScript()
                .EmitLoad(1, new BigInteger(10))
                .Emit(Opcode.JMP, new byte[] { 1, 2 })
                .EndScript();
        var sut = new Disassembler(script);
        
        //Assert.Equal("000: LOAD r1, 10\n005: JMP\n008: RET", sut.ToString());

        var instructions = sut.Instructions.ToArray();
        Assert.Equal(instructions.Length, 3);
        Assert.Equal(instructions[0].Opcode, Opcode.LOAD);
        Assert.Equal(instructions[0].Args.Length, 3);
        //Assert.Equal(instructions[0].Args[2], 10);
        Assert.Equal(instructions[1].Opcode, Opcode.JMP);
        //Assert.Equal(instructions[1].Args[0], 513);
        Assert.Equal(instructions[2].Opcode, Opcode.RET);

        script = ScriptUtils.BeginScript()
            .EmitLoad(1, new BigInteger(10))
            .Emit(Opcode.JMPNOT, new byte[] { 1, 32, 1 })
            .EndScript();
        
        sut = new Disassembler(script);

        instructions = sut.Instructions.ToArray();
        Assert.Equal(instructions.Length, 3);
        Assert.Equal(instructions[0].Opcode, Opcode.LOAD);
        Assert.Equal(instructions[0].Args.Length, 3);
        //Assert.Equal(instructions[0].Args[2], 10);
        Assert.Equal(instructions[1].Opcode, Opcode.JMPNOT);
        //Assert.Equal(instructions[1].Args[0], 513);
        Assert.Equal(instructions[2].Opcode, Opcode.RET);

        //Assert.Equal("000: LOAD r1, 10\n005: JMPNOT\n009: RET", sut.ToString());
    }

    [Fact]
    public void ReadVar_internal_should_read_32_and_64()
    {
        // Arrange
        ulong value = 0xFF34567890ABCDEF;
        ulong value2 = 0xFF34567890ABCDFF;
        var bytes = new byte[] { (int)Opcode.RANGE, 1, 2, 0, 1 };
        var script = ScriptUtils.BeginScript()
            .EmitRaw(new byte[]{ (int)Opcode.RIGHT, 0, 5, 0xFF, 1, 1, 0, 0, 0, 0, 0, 0})
            .EmitRaw(new byte[]{ (int)Opcode.RIGHT, 6, 11, 0xFE, 1, 1, 0, 0})
            //.EmitVarBytes(10)
            //.Emit(Opcode.RIGHT, new byte[] { 1, 5 })
            .EndScript();
        var sut = new Disassembler(script);

        // Act
        var result = Should.NotThrow(() => sut.ToString());

        // Assert
        result.ShouldNotBeNull();
    }


    [Fact]
    public void DetectFakeCallsInScripts()
    {
        var scriptBuilder = ScriptUtils.BeginScript();

        var rndAddress = PhantasmaKeys.Generate().Address;
        BigInteger price = 10000;
        BigInteger limit = 9999;

        scriptBuilder.EmitJump(Opcode.JMP, "call_skip");
        //AllowGas(Address from, Address target, BigInteger price, BigInteger limit)
        scriptBuilder.CallContract(NativeContractKind.Gas, nameof(GasContract.AllowGas), rndAddress, Address.Null, price, limit);
        scriptBuilder.EmitLabel("call_skip");

        var script = scriptBuilder.EndScript();

        // here we should detect the method, since we're ignoring jumps
        var methods1 = DisasmUtils.ExtractMethodCalls(script, DomainSettings.LatestKnownProtocol, detectAndUseJumps: false).ToArray();
        Assert.Equal(1, methods1.Length);

        // here we should not detect the method, since we're taking jumps into account
        var methods2 = DisasmUtils.ExtractMethodCalls(script, DomainSettings.LatestKnownProtocol, detectAndUseJumps: true).ToArray();
        Assert.Equal(0, methods2.Length);
    }

    [Fact]
    public void DetectLoopsInScripts()
    {
        var scriptBuilder = ScriptUtils.BeginScript();

        var rndAddress = PhantasmaKeys.Generate().Address;
        BigInteger price = 10000;
        BigInteger limit = 9999;

        scriptBuilder.EmitLabel("call_skip");
        //AllowGas(Address from, Address target, BigInteger price, BigInteger limit)
        scriptBuilder.CallContract(NativeContractKind.Gas, nameof(GasContract.AllowGas), rndAddress, Address.Null, price, limit);
        scriptBuilder.EmitJump(Opcode.JMP, "call_skip");

        var script = scriptBuilder.EndScript();

        Assert.Throws<ChainException>(() =>
        {
            // here we should not detect the method, since we're taking jumps into account
            DisasmUtils.ExtractMethodCalls(script, DomainSettings.LatestKnownProtocol, detectAndUseJumps: true).ToArray();
        });
    }

}
