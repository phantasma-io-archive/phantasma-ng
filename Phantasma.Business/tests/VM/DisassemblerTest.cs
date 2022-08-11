using Xunit;

namespace Phantasma.Business.Tests.VM;

public class DisassemblerTest
{
    
    [Fact]
    public void null_disassembler_test()
    {
        Disassembler disassembler = new Disassembler(new byte[]{0,5,10});
    }
    
    [Fact]
    public void getinstructions_disassembler_test()
    {
        Disassembler disassembler = new Disassembler(new byte[]{0,5,10});
    }


    
    [Fact]
    public void tostring_disassembler_test()
    {
        Disassembler disassembler = new Disassembler(new byte[]{0,5,10});
    }


    [Fact]
    public void read8_disassembler_test()
    {
        Disassembler disassembler = new Disassembler(new byte[]{0,5,10});
    }
    
    [Fact]
    public void read16_disassembler_test()
    {
        
    }
    
    [Fact]
    public void read32_disassembler_test()
    {
        
    }
    
    [Fact]
    public void read64_disassembler_test()
    {
        
    }
    
    [Fact]
    public void readbytes_disassembler_test()
    {
        
    }
}
