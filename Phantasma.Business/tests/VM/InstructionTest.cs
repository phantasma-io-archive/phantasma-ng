using System;
using System.Text;
using Phantasma.Business.VM;
using Phantasma.Core;
using Phantasma.Core.Domain;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.VM;

[Collection("InstructionTest")]
public class InstructionTest
{
    [Fact]
    public void Default_instruction_should_return_nop()
    {
        // Arrange
        var instruction = new Instruction();

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe("000: NOP");
    }

    [Theory]
    [InlineData(Opcode.MOVE)]
    [InlineData(Opcode.COPY)]
    [InlineData(Opcode.SWAP)]
    [InlineData(Opcode.SIZE)]
    [InlineData(Opcode.SIGN)]
    [InlineData(Opcode.NOT)]
    [InlineData(Opcode.NEGATE)]
    [InlineData(Opcode.ABS)]
    [InlineData(Opcode.CTX)]
    public void Context_should_throw_when_no_args_are_provided(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = Array.Empty<object>() };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Index was outside the bounds of the array.");
    }

    [Theory]
    [InlineData(Opcode.MOVE)]
    [InlineData(Opcode.COPY)]
    [InlineData(Opcode.SWAP)]
    [InlineData(Opcode.SIZE)]
    [InlineData(Opcode.SIGN)]
    [InlineData(Opcode.NOT)]
    [InlineData(Opcode.NEGATE)]
    [InlineData(Opcode.ABS)]
    [InlineData(Opcode.CTX)]
    public void Context_should_throw_with_invalid_arg_type(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { "arg1" } };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Unable to cast object of type 'System.String' to type 'System.Byte'.");
    }

    [Theory]
    [InlineData(Opcode.MOVE)]
    [InlineData(Opcode.COPY)]
    [InlineData(Opcode.SWAP)]
    [InlineData(Opcode.SIZE)]
    [InlineData(Opcode.SIGN)]
    [InlineData(Opcode.NOT)]
    [InlineData(Opcode.NEGATE)]
    [InlineData(Opcode.ABS)]
    [InlineData(Opcode.CTX)]
    public void Context_should_return_valid_instruction(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { (byte)0, (byte)1 } };

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe($"000: {opcode} r0, r1");
    }

    [Theory]
    [InlineData(Opcode.ADD)]
    [InlineData(Opcode.SUB)]
    [InlineData(Opcode.MUL)]
    [InlineData(Opcode.DIV)]
    [InlineData(Opcode.MOD)]
    [InlineData(Opcode.SHR)]
    [InlineData(Opcode.SHL)]
    [InlineData(Opcode.MIN)]
    [InlineData(Opcode.MAX)]
    public void Math_should_throw_when_no_args_are_provided(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = Array.Empty<object>() };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Index was outside the bounds of the array.");
    }

    [Theory]
    [InlineData(Opcode.ADD)]
    [InlineData(Opcode.SUB)]
    [InlineData(Opcode.MUL)]
    [InlineData(Opcode.DIV)]
    [InlineData(Opcode.MOD)]
    [InlineData(Opcode.SHR)]
    [InlineData(Opcode.SHL)]
    [InlineData(Opcode.MIN)]
    [InlineData(Opcode.MAX)]
    public void Math_should_throw_with_invalid_arg_type(Opcode opcode)
    {
        // Arrange
        var instruction =
            new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { "arg1", "arg2", "arg3" } };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Unable to cast object of type 'System.String' to type 'System.Byte'.");
    }

    [Theory]
    [InlineData(Opcode.ADD)]
    [InlineData(Opcode.SUB)]
    [InlineData(Opcode.MUL)]
    [InlineData(Opcode.DIV)]
    [InlineData(Opcode.MOD)]
    [InlineData(Opcode.SHR)]
    [InlineData(Opcode.SHL)]
    [InlineData(Opcode.MIN)]
    [InlineData(Opcode.MAX)]
    public void Math_should_return_valid_instruction(Opcode opcode)
    {
        // Arrange
        var instruction =
            new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { (byte)0, (byte)1, (byte)2 } };

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe($"000: {opcode} r0, r1, r2");
    }

    [Theory]
    [InlineData(Opcode.POP)]
    [InlineData(Opcode.PUSH)]
    [InlineData(Opcode.EXTCALL)]
    [InlineData(Opcode.DEC)]
    [InlineData(Opcode.INC)]
    [InlineData(Opcode.SWITCH)]
    public void Array_should_throw_when_no_args_are_provided(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = Array.Empty<object>() };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Index was outside the bounds of the array.");
    }

    [Theory]
    [InlineData(Opcode.POP)]
    [InlineData(Opcode.PUSH)]
    [InlineData(Opcode.EXTCALL)]
    [InlineData(Opcode.DEC)]
    [InlineData(Opcode.INC)]
    [InlineData(Opcode.SWITCH)]
    public void Array_should_throw_with_invalid_arg_type(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { "arg1" } };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Unable to cast object of type 'System.String' to type 'System.Byte'.");
    }

    [Theory]
    [InlineData(Opcode.POP)]
    [InlineData(Opcode.PUSH)]
    [InlineData(Opcode.EXTCALL)]
    [InlineData(Opcode.DEC)]
    [InlineData(Opcode.INC)]
    [InlineData(Opcode.SWITCH)]
    public void Array_should_return_valid_instruction(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { (byte)0 } };

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe($"000: {opcode} r0");
    }

    [Theory]
    [InlineData(Opcode.CALL)]
    public void Call_should_throw_when_no_args_are_provided(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = Array.Empty<object>() };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Index was outside the bounds of the array.");
    }

    [Theory]
    [InlineData(Opcode.CALL)]
    public void Call_should_throw_with_invalid_arg_type(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { "arg1", "arg2" } };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Unable to cast object of type 'System.String' to type 'System.Byte'.");
    }

    [Theory]
    [InlineData(Opcode.CALL)]
    public void Call_should_return_valid_instruction(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { (byte)0, (ushort)1 } };

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe($"000: {opcode} r0, 1");
    }

    [Theory]
    [InlineData(Opcode.LOAD)]
    public void Load_should_throw_when_no_args_are_provided(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = Array.Empty<object>() };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Index was outside the bounds of the array.");
    }

    [Theory]
    [InlineData(Opcode.LOAD)]
    public void Load_should_throw_with_invalid_arg_type(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction { Offset = 0, Opcode = opcode, Args = new object[] { "arg1" } };

        // Act
        var result = Should.Throw<Exception>(() => instruction.ToString());

        // Assert
        result.Message.ShouldBe("Unable to cast object of type 'System.String' to type 'System.Byte'.");
    }

    [Theory]
    [InlineData(Opcode.LOAD)]
    public void Load_should_return_valid_instruction_with_string(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction
        {
            Offset = 0,
            Opcode = opcode,
            Args = new object[] { (byte)0, VMType.String, Encoding.UTF8.GetBytes("test") }
        };

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe($"000: {opcode} r0, \"test\"");
    }

    [Theory]
    [InlineData(Opcode.LOAD)]
    public void Load_should_return_valid_instruction_with_number(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction
        {
            Offset = 0, Opcode = opcode, Args = new object[] { (byte)0, VMType.Number, new byte[] { 2 } }
        };

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe($"000: {opcode} r0, 2");
    }

    [Theory]
    [InlineData(Opcode.LOAD)]
    public void Load_should_return_valid_instruction_with_bool(Opcode opcode)
    {
        // Arrange
        var instruction = new Instruction
        {
            Offset = 0, Opcode = opcode, Args = new object[] { (byte)0, VMType.Bool, BitConverter.GetBytes(true) }
        };

        // Act
        var result = instruction.ToString();

        // Assert
        result.ShouldBe($"000: {opcode} r0, 01");
    }
}
