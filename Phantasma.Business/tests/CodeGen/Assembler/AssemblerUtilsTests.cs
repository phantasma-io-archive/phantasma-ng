using System;
using System.Collections.Generic;
using Phantasma.Business.CodeGen;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Business.VM;
using Phantasma.Core.Domain.VM.Structs;
using Xunit;

namespace Phantasma.Business.Tests.CodeGen.Assembler;

public class AssemblerUtilsTests
{
    [Fact (Skip = "Fix this test")]
    public void TestBuildScriptWithValidAssembly()
    {
        // Arrange
        var lines = new string[]
        {
            "mov ax, 10",
            "mov bx, 20",
            "add ax, bx",
        };
        var expected = new byte[] { 0xB8, 0x0A, 0x00, 0x00, 0x00, 0xBB, 0x14, 0x00, 0x00, 0x00, 0x01, 0xD8 };

        // Act
        var result = AssemblerUtils.BuildScript(lines);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestBuildScriptWithEmptyAssembly()
    {
        // Arrange
        var lines = new string[] { };

        var expected = new byte[] { };

        // Act
        var result = AssemblerUtils.BuildScript(lines);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestBuildScriptWithInvalidAssembly()
    {
        // Arrange
        var lines = new string[]
        {
            "mov ax, 10",
            "invalid instruction",
            "mov bx, 20",
            "add ax, bx"
        };

        var expection = "Error assembling the script: Phantasma.Business.CodeGen.CompilerException: ERROR: syntax error in line 1.\n";
        
        // Act and assert
        var ex = Assert.Throws<Exception>(() => AssemblerUtils.BuildScript(lines));
    }

    [Fact]
    public void TestCommentOffsetsWithValidAssemblyAndDebugInfo()
    {
        // Arrange
        var lines = new string[]
        {
            "mov ax, 10",
            "mov bx, 20",
            "add ax, bx"
        };

        var debugInfo = new DebugInfo("test.asm", new List<DebugRange>()
        {
            new DebugRange(1, 0, 5),
            new DebugRange(2, 6, 11),
            new DebugRange(3, 12, 15)
        });

        var expected = new string[]
        {
            "mov ax, 10 // 0",
            "mov bx, 20 // 6",
            "add ax, bx // 12"
        };

        // Act
        var result = AssemblerUtils.CommentOffsets(lines, debugInfo);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestCommentOffsetsWithEmptyAssemblyAndDebugInfo()
    {
        // Arrange
        var lines = new string[] { };

        var debugInfo = new DebugInfo("test.asm", new List<DebugRange>() { });

        var expected = new string[] { };

        // Act
        var result = AssemblerUtils.CommentOffsets(lines, debugInfo);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestCommentOffsetsWithAssemblyAndInvalidDebugInfo()
    {
        // Arrange
        var lines = new string[]
        {
            "mov ax, 10",
            "mov bx, 20",
            "add ax, bx"
        };

        var debugInfo = new DebugInfo("test.asm", new List<DebugRange>()
        {
            new DebugRange(1, 0, 5),
            new DebugRange(3, 12, 15)
        });

        var expected = new string[]
        {
            "mov ax, 10 // 0",
            "mov bx, 20",
            "add ax, bx // 12"
        };

        // Act
        var result = AssemblerUtils.CommentOffsets(lines, debugInfo);

        // Assert
        Assert.Equal(expected, result);
    }
}