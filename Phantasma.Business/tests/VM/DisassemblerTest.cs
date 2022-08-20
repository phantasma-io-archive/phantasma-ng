using System;
using System.Linq;
using Phantasma.Business.VM;
using Phantasma.Core;
using Phantasma.Core.Domain;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.VM;

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
    }

    [Theory]
    [InlineData(new byte[] { (int)Opcode.CTX, 1, 2 }, "000: CTX r1, r2")]
    [InlineData(new byte[] { (int)Opcode.MOVE, 100, 7 }, "000: MOVE r100, r7")]
    [InlineData(new byte[] { (int)Opcode.COPY, 255, 254 }, "000: COPY r255, r254")]
    [InlineData(new byte[] { (int)Opcode.SWAP, 4, 8 }, "000: SWAP r4, r8")]
    [InlineData(new byte[] { (int)Opcode.RET }, "000: RET")]
    [InlineData(new byte[] { (int)Opcode.POP, 1 }, "000: POP r1")]
    [InlineData(new byte[] { (int)Opcode.PUSH, 2 }, "000: PUSH r2")]
    [InlineData(new byte[] { (int)Opcode.EXTCALL, 3 }, "000: EXTCALL r3")]
    //[InlineData(new byte[] { (int)Opcode.THROW, 4 }, "000: THROW r4")]
    [InlineData(new byte[] { (int)Opcode.INC, 78 }, "000: INC r78")]
    [InlineData(new byte[] { (int)Opcode.DEC, 45 }, "000: DEC r45")]
    [InlineData(new byte[] { (int)Opcode.SWITCH, 33 }, "000: SWITCH r33")]
    //[InlineData(new byte[] { (int)Opcode.AND, 1, 1, 1 }, "000: AND r1, r1, r1")]
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
    public void ToString_for_opcode_load_should_return_expected_value()
    {
        // Arrange
        var sut = new Disassembler(new ScriptBuilder().EmitLoad(0, new byte[] { 112, 23 }, VMType.Number).EmitPush(0)
            .ToScript());

        // Act
        var result = sut.ToString();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe($"000: LOAD r0, 6000{Environment.NewLine}006: PUSH r0");
    }
}
