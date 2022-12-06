namespace Phantasma.Core.Tests.Utils;

using Phantasma.Core.Utils;
using Xunit;

public class BitUtilsTests
{
    [Fact]
    public void TestBitUtils()
    {
        var offset = 0;
        var buffer = new byte[16];
        var buffer2 = new byte[16];
        var length = 8;
        short data = 2;
    
        BitUtils.WriteLittleEndian(buffer, offset, data);
        BitUtils.GetBytes(buffer2, 0, data);
        Assert.Equal(buffer2, buffer);
    }
    
    [Fact]
    public void TestBitUtils2()
    {
        var offset = 0;
        var buffer = new byte[16];
        var buffer2 = new byte[16];
        var length = 8;
        short data = 10;
    
        BitUtils.WriteLittleEndian(buffer, offset, data);
        BitUtils.WriteLittleEndian(buffer, offset+length, data);
        BitUtils.GetBytes(buffer2, offset, data);
        BitUtils.GetBytes(buffer2, offset+length, data);
        Assert.Equal(buffer2, buffer);
    }
}
