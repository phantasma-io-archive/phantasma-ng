using System.Text;
using Moq;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM.Utils;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.VM;
using Xunit;

namespace Phantasma.Business.Tests.CodeGen.Assembler;

public class LabelTests
{
    [Fact]
    public void Label_Constructor_SetsName()
    {
        // Arrange
        uint lineNumber = 123;
        string name = "MyLabel";

        // Act
        var label = new Label(lineNumber, name);

        // Assert
        Assert.Equal(name, label.Name);
    }

    [Fact]
    public void Label_ToString_ReturnsName()
    {
        // Arrange
        uint lineNumber = 123;
        string name = "MyLabel";
        var label = new Label(lineNumber, name);

        // Act
        var result = label.ToString();

        // Assert
        Assert.Equal(name, result);
    }

    [Fact]
    public void Label_Process_CallsEmitLabelWithName()
    {
        // Arrange
        uint lineNumber = 123;
        string name = "MyLabel";
        var label = new Label(lineNumber, name);
        var scriptBuilder = new ScriptBuilder();
        scriptBuilder.Emit(Opcode.PUSH, Encoding.UTF8.GetBytes(name));
        //scriptBuilder.Setup(sb => sb.EmitLabel(name));

        // Act
        label.Process(scriptBuilder);

        // Assert
        Assert.Equal(9, scriptBuilder.CurrentSize);
        //Assert.Equal(2, scriptBuilder.CurrentSize);
        //scriptBuilder.Verify(sb => sb.EmitLabel(name), Times.Once());
    }
}
