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
    
    [Fact (Skip = "fix this later")]
    public void GetBytes_ShouldConvertDoubleToLittleEndianByteArray()
    {
        // Arrange
        var value = 1234.5678;
        var expected = new byte[] { 0x74, 0x33, 0x6B, 0x3F, 0xD0, 0x00, 0x00, 0x00 };
        var bytes = new byte[8];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }
    
    [Fact (Skip = "fix this later")]
    public void GetBytes_ShouldConvertFloatToLittleEndianByteArray()
    {
        // Arrange
        var value = 123.456f;
        var expected = new byte[] { 0x49, 0xC3, 0xF5, 0x40 };
        var bytes = new byte[4];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void GetBytes_ShouldConvertShortToLittleEndianByteArray()
    {
        // Arrange
        var value = (short)1234;
        var expected = new byte[] { 0xD2, 0x04 };
        var bytes = new byte[2];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void GetBytes_ShouldConvertUShortToLittleEndianByteArray()
    {
        // Arrange
        var value = (ushort)1234;
        var expected = new byte[] { 0xD2, 0x04 };
        var bytes = new byte[2];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }

    [Fact (Skip = "fix this later")]
    public void GetBytes_ShouldConvertIntToLittleEndianByteArray()
    {
        // Arrange
        var value = 12345678;
        var expected = new byte[] { 0x2E, 0xBC, 0x61, 0x00 };
        var bytes = new byte[4];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }

    [Fact (Skip = "fix this later")]
    public void GetBytes_ShouldConvertUIntToLittleEndianByteArray()
    {
        // Arrange
        var value = (uint)12345678;
        var expected = new byte[] { 0x2E, 0xBC, 0x61, 0x00 };
        var bytes = new byte[4];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }

    [Fact (Skip = "fix this later")]
    public void GetBytes_ShouldConvertLongToLittleEndianByteArray()
    {
        // Arrange
        var value = 123456789012345;
        var expected = new byte[] { 0x15, 0xCD, 0x5B, 0x07, 0xE1, 0x41, 0x00, 0x00 };
        var bytes = new byte[8];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }
    
    [Fact (Skip = "fix this later")]
    public void GetBytes_ShouldConvertULongToLittleEndianByteArray()
    {
        // Arrange
        var value = (ulong)123456789012345;
        var expected = new byte[] { 0x15, 0xCD, 0x5B, 0x07, 0xE1, 0x41, 0x00, 0x00 };
        var bytes = new byte[8];

        // Act
        BitUtils.GetBytes(bytes, 0, value);

        // Assert
        Assert.Equal(expected, bytes);
    }
}
