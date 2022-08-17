using Xunit;

namespace Phantasma.Business.Tests.VM;

public class DisassemblerTest
{
    [Fact(Skip = "Test needs to be fixed")]
    public void null_disassembler_test()
    {
        var disassembler = new Disassembler(new byte[] { 0, 5, 10 });
    }

    [Fact(Skip = "Test needs to be fixed")]
    public void getinstructions_disassembler_test()
    {
        var disassembler = new Disassembler(new byte[] { 0, 5, 10 });
    }

    [Fact(Skip = "Test needs to be fixed")]
    public void tostring_disassembler_test()
    {
        var disassembler = new Disassembler(new byte[] { 0, 5, 10 });
    }

    [Fact(Skip = "Test needs to be fixed")]
    public void read8_disassembler_test()
    {
        var disassembler = new Disassembler(new byte[] { 0, 5, 10 });
    }

    [Fact(Skip = "Test needs to be fixed")]
    public void read16_disassembler_test()
    {
    }

    [Fact(Skip = "Test needs to be fixed")]
    public void read32_disassembler_test()
    {
    }

    [Fact(Skip = "Test needs to be fixed")]
    public void read64_disassembler_test()
    {
    }

    [Fact(Skip = "Test needs to be fixed")]
    public void readbytes_disassembler_test()
    {
    }
}
