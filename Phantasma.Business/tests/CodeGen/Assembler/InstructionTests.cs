using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore.Internal;
using Phantasma.Business.CodeGen;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Xunit;

namespace Phantasma.Business.Tests.CodeGen.Assembler;

public class InstructionTests
{
    /*
     * Note that in order for these tests to actually run,
     * you will need to provide implementations for the scriptBuilder object,
     * as well as the expected results for each test.
     */
    [Fact]
    public void TestProcess1Reg()
    {
        // Arrange
        var arguments = new string[] { "r1" };
        var instruction = new Instruction(1, "push", arguments);
        var scriptBuilder = new ScriptBuilder();
        var scriptBuilder2 = new ScriptBuilder();
        
        
        // Act
        scriptBuilder.EmitPush(1);
        scriptBuilder2.EmitPush(1);
        instruction.Process(scriptBuilder);
        
        // Assert
        Assert.Equal("push", instruction.ToString());
        Assert.Equal(arguments.Length, instruction.Arguments.Length);
        Assert.NotEqual(scriptBuilder, scriptBuilder2);
        Assert.NotEqual(scriptBuilder.CurrentSize, scriptBuilder2.CurrentSize);
    }

    [Fact]
    public void TestProcess2Reg()
    {
        // Arrange
        var arguments = new string[] { "r1", "r2"};
        var instruction = new Instruction(1, "swap", arguments);
        var scriptBuilder = new ScriptBuilder();
        var scriptBuilder2 = new ScriptBuilder();
        var swapBytes = Encoding.UTF8.GetBytes("r1 r2");
        
        // Act
        scriptBuilder.Emit(Opcode.SWAP, swapBytes);
        scriptBuilder2.Emit(Opcode.SWAP, swapBytes);
        instruction.Process(scriptBuilder);
        
        

        // Assert
        Assert.Equal("swap", instruction.ToString());
        Assert.NotEqual(scriptBuilder, scriptBuilder2);
        Assert.NotEqual(scriptBuilder.CurrentSize, scriptBuilder2.CurrentSize);
    }

    [Fact]
    public void TestProcess3Reg()
    {
        // Arrange
        var arguments = new string[] { "r1", "r2", "r3"};
        var instruction = new Instruction(1, "add", arguments);
        var scriptBuilder = new ScriptBuilder();
        var scriptBuilder2 = new ScriptBuilder();
        var swapBytes = Encoding.UTF8.GetBytes("r1 r2 r3");

        // Act
        scriptBuilder.Emit(Opcode.ADD, swapBytes);
        scriptBuilder2.Emit(Opcode.ADD, swapBytes);
        instruction.Process(scriptBuilder);

        // Assert
        Assert.Equal("add", instruction.ToString());
        Assert.NotEqual(scriptBuilder, scriptBuilder2);
        Assert.NotEqual(scriptBuilder.CurrentSize, scriptBuilder2.CurrentSize);
    }
    
    [Theory]
    [InlineData(new string[]{"r1"}, "", true)]
    [InlineData(new string[]{"0", "7", "7"}, "load", true)]
    [InlineData(new string[]{"r1", "0" ,"0", "0"}, "load", true)]
    [InlineData(new string[]{"r1", "null"}, "load", true)]
    [InlineData(new string[]{"r1", "7" ,"7"}, "load")]
    [InlineData(new string[]{"r1", "7" ,"0"}, "load")]
    [InlineData(new string[]{"r1", "0x11"}, "load")]
    [InlineData(new string[]{"r1", "false"}, "load")]
    [InlineData(new string[]{"r1", "\"imString\""}, "load")]
    public void TestProcessLoad(string[] args, string expected, bool error = false)
    {
        // Arrange
        var arguments = args;
        var instruction = new Instruction(1, "load", arguments);
        var scriptBuilder = new ScriptBuilder();
        var scriptBuilder2 = new ScriptBuilder();
        var loadBytes = Encoding.UTF8.GetBytes(args.Join());

        // Act
        scriptBuilder.Emit(Opcode.LOAD, loadBytes);
        scriptBuilder2.Emit(Opcode.LOAD, loadBytes);

        if (error)
        {
            Assert.Throws<CompilerException>(() => instruction.Process(scriptBuilder));
            return;
        }

        instruction.Process(scriptBuilder);
        
        // Test
        Assert.Equal("load", instruction.ToString());
        Assert.NotEqual(scriptBuilder, scriptBuilder2);
        Assert.NotEqual(scriptBuilder.CurrentSize, scriptBuilder2.CurrentSize);
    }

    [Theory]
    [InlineData(new string[] { "r1", "asdas", "asdasd"}, "ret", true)]
    [InlineData(new string[] { "r1", "r3"}, "ret", true)]
    [InlineData(new string[] { "123123" }, "ret", true)]
    [InlineData(new string[] { "r1" }, "ret")]
    [InlineData(new string[] {  }, "ret")]
    public void Test_Return(string[] args, string expected, bool error = false)
    {
        var arguments = args;
        var instruction = new Instruction(1, "ret", arguments);
        var scriptBuilder = new ScriptBuilder();
        var scriptBuilder2 = new ScriptBuilder();
        var loadBytes = Encoding.UTF8.GetBytes(args.Join());

        // Act
        scriptBuilder.Emit(Opcode.RET, loadBytes);
        scriptBuilder2.Emit(Opcode.RET, loadBytes);

        if (error)
        {
            Assert.Throws<CompilerException>(() => instruction.Process(scriptBuilder));
            return;
        }

        instruction.Process(scriptBuilder);
        
        // Test
        Assert.Equal(expected, instruction.ToString());
        Assert.NotEqual(scriptBuilder, scriptBuilder2);
        Assert.NotEqual(scriptBuilder.CurrentSize, scriptBuilder2.CurrentSize);
    }
    
    [Theory]
    [InlineData(new string[] { "r1", "asdas", "asdasd"}, "switch", Opcode.SWITCH, true)]
    [InlineData(new string[] { "123" }, "switch", Opcode.SWITCH, true)]
    [InlineData(new string[] { "r1"}, "switch", Opcode.SWITCH)]
    [InlineData(new string[] { "r1", "r2", "r3"}, "ctx", Opcode.CTX, true)]
    [InlineData(new string[] { "r1", "123123"}, "ctx", Opcode.CTX, true)]
    [InlineData(new string[] { "123", "123123"}, "ctx", Opcode.CTX, true)]
    [InlineData(new string[] { "r1", "r2"}, "ctx", Opcode.CTX)]
    [InlineData(new string[] { "r1", "r2", "r3", "r4"}, "right", Opcode.RIGHT, true)]
    [InlineData(new string[] { "r1", "r2", "asd"}, "right", Opcode.RIGHT, true)]
    [InlineData(new string[] { "r1", "r2", "5"}, "right", Opcode.RIGHT)]
    [InlineData(new string[] { "r1", "r2"}, "jmp", Opcode.JMP, true)]
    [InlineData(new string[] { "r1" }, "jmp", Opcode.JMP, true)]
    [InlineData(new string[] { "@1" }, "jmp", Opcode.JMP)]
    [InlineData(new string[] { "@1", "@ad", "@as" }, "jmpif", Opcode.JMPIF, true)]
    [InlineData(new string[] { "@1", "@ad" }, "jmpif", Opcode.JMPIF, true)]
    [InlineData(new string[] { "r1", "@ad" }, "jmpif", Opcode.JMPIF)]
    [InlineData(new string[] {  }, "call", Opcode.CALL, true)]
    [InlineData(new string[] { "r1"  }, "call", Opcode.CALL, true)]
    [InlineData(new string[] { "@1", "0x1"  }, "call", Opcode.CALL, true)]
    [InlineData(new string[] { "@1", "3"  }, "call", Opcode.CALL)]
    [InlineData(new string[] { "@1", "3"  }, "extcall", Opcode.EXTCALL, true)]
    [InlineData(new string[] { "@1" }, "extcall", Opcode.EXTCALL, true)]
    [InlineData(new string[] { "\"test\"" }, "extcall", Opcode.EXTCALL)]
    [InlineData(new string[] { "r1" }, "extcall", Opcode.EXTCALL)]
    [InlineData(new string[] { "r1" }, "range", Opcode.RANGE, true)]
    [InlineData(new string[] { "r1", "@2", "5", "5" }, "range", Opcode.RANGE, true)]
    [InlineData(new string[] { "r1", "r2", "0x5", "5" }, "range", Opcode.RANGE, true)]
    [InlineData(new string[] { "r1", "r2", "5", "5" }, "range", Opcode.RANGE)]
    [InlineData(new string[] { "r1", "r2", "5", "5" }, "cast", Opcode.CAST, true)]
    [InlineData(new string[] { "0x00", "r2", "5" }, "cast", Opcode.CAST, true)]
    [InlineData(new string[] { "r1", "0x0", "5" }, "cast", Opcode.CAST, true)]
    [InlineData(new string[] { "r1", "r2", "5" }, "cast", Opcode.CAST, true)]
    [InlineData(new string[] { "r1", "r2", "#5" }, "cast", Opcode.CAST)]
    [InlineData(new string[] { "r1", "r2", "#5" }, "alias", null, true)]
    [InlineData(new string[] { "0x0", "r2"}, "alias", null, true)]
    [InlineData(new string[] { "r1", "r2"}, "alias", null, true)]
    [InlineData(new string[] { "r1", "r2"}, "alias", null, true)]
    public void Test_Global(string[] args, string expected, Opcode code, bool error = false)
    {
        var arguments = args;
        var instruction = new Instruction(1, expected, arguments);
        var scriptBuilder = new ScriptBuilder();
        var scriptBuilder2 = new ScriptBuilder();
        var loadBytes = Encoding.UTF8.GetBytes(args.Join());

        // Act
        scriptBuilder.Emit(code, loadBytes);
        scriptBuilder2.Emit(code, loadBytes);

        if (error)
        {
            Assert.Throws<CompilerException>(() => instruction.Process(scriptBuilder));
            return;
        }

        instruction.Process(scriptBuilder);
        
        // Test
        Assert.Equal(expected, instruction.ToString());
        Assert.NotEqual(scriptBuilder, scriptBuilder2);
        Assert.NotEqual(scriptBuilder.CurrentSize, scriptBuilder2.CurrentSize);
    }

    [Fact]
    public void Test_Alias()
    {
        var arguments = new string[] {"r1", "$a"};
        var instruction = new Instruction(0, "alias", arguments);
        var scriptBuilder = new ScriptBuilder();
        var scriptBuilder2 = new ScriptBuilder();
        var loadBytes = Encoding.UTF8.GetBytes(arguments.Join());

        // Act
        scriptBuilder.Emit((Opcode) 999, loadBytes);
        scriptBuilder2.Emit((Opcode) 999, loadBytes);

        instruction.Process(scriptBuilder);
        
        // Test
        Assert.Equal("alias", instruction.ToString());
        Assert.NotEqual(scriptBuilder, scriptBuilder2);
    }
}
