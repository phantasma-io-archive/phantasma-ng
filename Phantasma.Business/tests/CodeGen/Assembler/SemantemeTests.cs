using System;
using System.IO;
using Phantasma.Business.CodeGen.Assembler;
using Phantasma.Core.Utils;
using Xunit;

namespace Phantasma.Business.Tests.CodeGen.Assembler;

public class SemantemeTests
{
    [Fact]
    public void TestProcessLines()
    {
        // Test empty input
        var semantemes = Semanteme.ProcessLines(new string[] {});
        Assert.Empty(semantemes);

        // Test input with only whitespace
        semantemes = Semanteme.ProcessLines(new string[] {"   ", "  \t  ", "\n"});
        Assert.Empty(semantemes);

        // Test input with only comments
        semantemes = Semanteme.ProcessLines(new string[] {
            "// this is a comment",
            "  // this is another comment",
            "/* this is a multi-line comment */"
        });
        Assert.Empty(semantemes);

        // Test input with only labels
        semantemes = Semanteme.ProcessLines(new string[] {
            "label1:",
            "  label2:",
            "\tlabel3:\t"
        });
        Assert.Equal(3, semantemes.Length);
        Assert.IsType<Label>(semantemes[0]);
        Assert.IsType<Label>(semantemes[1]);
        Assert.IsType<Label>(semantemes[2]);

        // Test input with only instructions
        semantemes = Semanteme.ProcessLines(new string[] {
            "instr1 arg1 arg2",
            "  instr2 arg1 arg2 arg3",
            "\tinstr3\targ1\targ2\targ3\targ4"
        });
        Assert.Equal(3, semantemes.Length);
        Assert.IsType<Instruction>(semantemes[0]);
        Assert.IsType<Instruction>(semantemes[1]);
        Assert.IsType<Instruction>(semantemes[2]);

        // Test input with labels and instructions
        semantemes = Semanteme.ProcessLines(new string[] {
            "label1:",
            "instr1 arg1 arg2",
            "  label2:",
            "  instr2 arg1 arg2 arg3",
            "\tinstr3\targ1\targ2\targ3\targ4"
        });
        Assert.Equal(5, semantemes.Length);
        Assert.IsType<Label>(semantemes[0]);
        Assert.IsType<Instruction>(semantemes[1]);
        Assert.IsType<Label>(semantemes[2]);
        Assert.IsType<Instruction>(semantemes[3]);
        Assert.IsType<Instruction>(semantemes[4]);
    }
    
    [Fact]
    public void WriteVarInt_ReadVarInt_ValidValues()
    {
        long[] testValues = { 0, 0x10, 0xFD, 0xFE, 0x1000, 0xFFFF, 0xFFFFFFFF, 0x100000000L, long.MaxValue };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        using var reader = new BinaryReader(ms);

        foreach (long value in testValues)
        {
            ms.Position = 0;
            writer.WriteVarInt(value);
            ms.Position = 0;
            ulong result = reader.ReadVarInt();
            Assert.Equal((ulong)value, result);
        }
    }
    
    [Fact]
    public void WriteVarInt_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteVarInt(-1));
    }

    // Add more tests for other methods like WriteBigInteger, WriteTimestamp, WriteByteArray, WriteVarString, etc.

    // Example test for WriteByteArray and ReadByteArray
    [Fact]
    public void WriteByteArray_ReadByteArray_ValidByteArray()
    {
        byte[] testArray = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        using var reader = new BinaryReader(ms);

        writer.WriteByteArray(testArray);
        ms.Position = 0;
        byte[] result = reader.ReadByteArray();

        Assert.Equal(testArray, result);
    }

    [Fact]
    public void WriteByteArray_ReadByteArray_EmptyByteArray()
    {
        byte[] testArray = Array.Empty<byte>();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        using var reader = new BinaryReader(ms);

        writer.WriteByteArray(testArray);
        ms.Position = 0;
        byte[] result = reader.ReadByteArray();

        Assert.Equal(testArray, result);
    }
}
