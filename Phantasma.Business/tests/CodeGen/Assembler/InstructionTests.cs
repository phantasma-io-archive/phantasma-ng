using System.Text;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Domain;
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
}
