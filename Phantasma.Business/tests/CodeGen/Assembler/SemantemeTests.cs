using Phantasma.Business.CodeGen.Assembler;
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
}
