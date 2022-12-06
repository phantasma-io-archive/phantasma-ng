using System.Linq;

namespace Phantasma.Core.Tests.Utils;
using Phantasma.Core.Utils;

using Xunit;

public class PackingUtilsTests
{
    // write tests based on the PackingUtils class and methods
    [Fact]
    public void TestBEUInt16()
    {
        // byte array that is equal to 10
        var bytes = new byte[] { 0x00, 0x0A };
        var packed = PackingUtils.BE_To_UInt16(bytes);
        Assert.Equal(10, packed);
    }
    
    [Fact]
    public void TestBEUInt16Offset()
    {
        // byte array that is equal to 10
        var bytes = new byte[]{  0x00, 0x00, 0x0A, 0x00};
        var offset = 1;
        var unpacked = PackingUtils.BE_To_UInt16(bytes, offset);
        ushort value = 10;
        Assert.Equal(value, unpacked);
    }
    
    [Fact]
    public void TestUInt16BE()
    {
        // byte array that is equal to 10
        var bytes = new byte[] {  0x00, 0x0A };
        var bytesOut = new byte[2];
        PackingUtils.UInt16_To_BE((ushort)10, bytesOut);
        Assert.Equal(bytes, bytesOut);
    }
    
    [Fact]
    public void TestUInt16BEOffset()
    {
        // byte array that is equal to 10
        var offset = 1;
        var bytes = new byte[4];
        PackingUtils.UInt16_To_BE((ushort)10, bytes, offset);
        ushort value = PackingUtils.BE_To_UInt16(bytes, offset);
        Assert.Equal(10, value);
    }
    
    [Fact]
    public void TestBEUInt32()
    {
        // byte array that is equal to 20
        var bytes = new byte[] {  0x00, 0x00, 0x00, 0x14 };
        var packed = PackingUtils.BE_To_UInt32(bytes);
        uint value = 20;
        Assert.Equal(value, packed);
        
        packed = PackingUtils.BE_To_UInt32(bytes, 0);
        Assert.Equal(value, packed);

        var bytesOutArray = PackingUtils.UInt32_To_BE(new uint[] { packed });
        Assert.Equal(bytes, bytesOutArray);
        
        uint[] values = new uint[] { 20 };
        PackingUtils.BE_To_UInt32(bytes, 0, values);
        Assert.Equal(value, values[0]);
        
        var bytesOut = new byte[4];
        PackingUtils.UInt32_To_BE(packed, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutOffset = new byte[5];
        PackingUtils.UInt32_To_BE(packed, bytesOutOffset, 1);
        Assert.Equal(bytes, bytesOutOffset.Skip(1));
        
    }
    
    [Fact]
    public void TestUInt32BE()
    {
        // byte array that is equal to 20
        var bytes = new byte[] {  0x00, 0x00, 0x00, 0x14 };
        var bytesOut = new byte[4];
        PackingUtils.UInt32_To_BE(20, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        PackingUtils.UInt32_To_BE((uint)20, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        var bytesUint = PackingUtils.UInt32_To_BE((uint)20);
        Assert.Equal(bytes, bytesUint);
        
        PackingUtils.UInt32_To_BE(20u, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        uint[] values = new uint[] { 20 };
        var bytesOutArray = PackingUtils.UInt32_To_BE(values);
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffset = new byte[5];
        PackingUtils.UInt32_To_BE(20, bytesOutOffset, 1);
        Assert.Equal(bytes, bytesOutOffset.Skip(1).ToArray());
    }

    [Fact]
    public void TestBEUInt64()
    {
        // byte array that is equal to 30
        var bytes = new byte[] {  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1E };
        var packed = PackingUtils.BE_To_UInt64(bytes);
        ulong value = 30;
        Assert.Equal(value, packed);
        
        packed = PackingUtils.BE_To_UInt64(bytes, 0);
        Assert.Equal(value, packed);
        
        
        packed = PackingUtils.BE_To_UInt64(bytes);
        Assert.Equal(value, packed);

        ulong[] ns = new ulong[1];
        PackingUtils.BE_To_UInt64(bytes, 0, ns);
        Assert.Equal(value, ns[0]);
        
        var bytesOutArray = PackingUtils.UInt64_To_BE(new ulong[] { packed });
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOut = new byte[8];
        PackingUtils.UInt64_To_BE(packed, bytesOut);
        Assert.Equal(bytes, bytesOut);
    }
    
    [Fact]
    public void TestUInt64BE()
    {
        // byte array that is equal to 30
        var bytes = new byte[] {  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1E };
        var bytesOut = new byte[8];
        PackingUtils.UInt64_To_BE(30, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);

        var bytesUlong = PackingUtils.UInt64_To_BE((ulong)30);
        Assert.Equal(bytes, bytesUlong);

        PackingUtils.UInt64_To_BE((ulong)30, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);
        
        PackingUtils.UInt64_To_BE(30u, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);
        
        ulong[] values = new ulong[] { 30 };
        var bytesOutArray = PackingUtils.UInt64_To_BE(values);
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffset = new byte[16];
        PackingUtils.UInt64_To_BE(30, bytesOutOffset, 1);
    }

    [Fact]
    public void TestLEUInt16()
    {
        // byte array that is equal to 10
        var bytes = new byte[] {  0x0A, 0x00 };
        var packed = PackingUtils.LE_To_UInt16(bytes);
        ushort value = 10;
        Assert.Equal(value, packed);

        packed = PackingUtils.LE_To_UInt16(bytes, 0);
        Assert.Equal(value, packed);
    }
    
    [Fact]
    public void TestUInt16LE()
    {
        // byte array that is equal to 10
        var bytes = new byte[] {  0x0A, 0x00 };
        var bytesOut = new byte[2];
        PackingUtils.UInt16_To_LE(10, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);
        
        PackingUtils.UInt16_To_LE(10, bytesOut);
        Assert.Equal(bytes, bytesOut);

        PackingUtils.UInt16_To_LE((ushort)10, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);
    }
    
    [Fact]
    public void TestLEUInt32()
    {
        // byte array that is equal to 20
        var bytes = new byte[] {  0x14, 0x00, 0x00, 0x00 };
        var packed = PackingUtils.LE_To_UInt32(bytes);
        uint value = 20;
        Assert.Equal(value, packed);
        
        packed = PackingUtils.LE_To_UInt32(bytes, 0);
        Assert.Equal(value, packed);
        
        
        
        uint[] ns = new uint[1];
        PackingUtils.LE_To_UInt32(bytes, 0, ns);
        Assert.Equal(value, ns[0]);
        
        var bytesOutArray = PackingUtils.UInt32_To_LE(new uint[] { packed });
        Assert.Equal(bytes, bytesOutArray);
    }
    
    [Fact]
    public void TestUInt32LE()
    {
        // byte array that is equal to 20
        var bytes = new byte[] {  0x14, 0x00, 0x00, 0x00 };
        var bytesOut = new byte[4];
        PackingUtils.UInt32_To_LE(20, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);
        
        PackingUtils.UInt32_To_LE((uint)20, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);
        
        PackingUtils.UInt32_To_LE(20u, bytesOut, 0);
        Assert.Equal(bytes, bytesOut);
        
        uint[] values = new uint[] { 20 };
        var bytesOutArray = PackingUtils.UInt32_To_LE(values);
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffset = new byte[5];
        PackingUtils.UInt32_To_LE(20, bytesOutOffset, 1);
        Assert.Equal(bytes, bytesOutOffset.Skip(1).ToArray());
    }
    
    [Fact]
    public void TestLEUInt64()
    {
        // byte array that is equal to 30
        var bytes = new byte[] {  0x1E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var packed = PackingUtils.LE_To_UInt64(bytes);
        ulong value = 30;
        Assert.Equal(value, packed);
        
        packed = PackingUtils.LE_To_UInt64(bytes, 0);
        Assert.Equal(value, packed);
        
        ulong[] ns = new ulong[1];
        PackingUtils.LE_To_UInt64(bytes, 0, ns);
        Assert.Equal(value, ns[0]);
        
        var bytesOutArray = PackingUtils.UInt64_To_LE(new ulong[] { packed });
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOut = new byte[8];
        PackingUtils.UInt64_To_LE(packed, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutOffset = new byte[9];
        PackingUtils.UInt64_To_LE(30, bytesOutOffset, 1);
        Assert.Equal(bytes, bytesOutOffset.Skip(1).ToArray());
        
        var bytesOutOffsetArray = new byte[9];
        PackingUtils.UInt64_To_LE(new ulong[] { 30 }, bytesOutOffsetArray, 1);
        Assert.Equal(bytes, bytesOutOffsetArray.Skip(1).ToArray());
    }
    
    [Fact]
    public void TestUInt64LE()
    {
        // byte array that is equal to 30
        var bytes = new byte[] {  0x1E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var bytesOut = new byte[8];
        PackingUtils.UInt64_To_LE(30, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        PackingUtils.UInt64_To_LE((ulong)30, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        PackingUtils.UInt64_To_LE(30uL, bytesOut);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = PackingUtils.UInt64_To_LE(new ulong[] { 30 });
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffset = new byte[9];
        PackingUtils.UInt64_To_LE(30, bytesOutOffset, 1);
        Assert.Equal(bytes, bytesOutOffset.Skip(1).ToArray());
    }


    [Fact]
    public void TestPackBEUInt32Offset()
    {
        // byte array that is equal to 20
        var bytes = new byte[]{  0x00, 0x00, 0x00, 0x00, 0x00, 0x14, 0x00, 0x00};
        var offset = 2;
        var unpacked = PackingUtils.BE_To_UInt32(bytes, offset);
        uint value = 20;
        Assert.Equal(value, unpacked);
        
        var bytesOut = new byte[8];
        PackingUtils.UInt32_To_BE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        //var bytesOutArray = PackingUtils.UInt32_To_BE(new uint[] { value });
        //Assert.Equal(bytes.Skip(offset), bytesOutArray);
        
        var bytesOutOffsetArray = new byte[8];
        PackingUtils.UInt32_To_BE(new uint[] { value }, bytesOutOffsetArray, offset);
        Assert.Equal(bytes, bytesOutOffsetArray);
    }
    
    [Fact]
    public void TestPackBEUInt64Offset()
    {
        // byte array that is equal to 30
        var bytes = new byte[]{ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1E, 0x00, 0x00, 0x00 };
        var offset = 3;
        var unpacked = PackingUtils.BE_To_UInt64(bytes, offset);
        ulong value = 30;
        Assert.Equal(value, unpacked);
        
        var bytesOut = new byte[14];
        PackingUtils.UInt64_To_BE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = new byte[14];
        PackingUtils.UInt64_To_BE(new ulong[] { value }, bytesOutArray, offset);
        Assert.Equal(bytes, bytesOutArray);
    }
    
    [Fact]
    public void TestLEUInt16Offset()
    {
        // byte array that is equal to 10
        var bytes = new byte[] {  0x00, 0x0A, 0x00, 0x00 };
        var offset = 1;
        var packed = PackingUtils.LE_To_UInt16(bytes, offset);
        ushort value = 10;
        Assert.Equal(value, packed);
    
    }
    
    [Fact]
    public void TestLEUInt32Offset()
    {
        // byte array that is equal to 20
        var bytes = new byte[] {  0x00, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var offset = 2;
        var packed = PackingUtils.LE_To_UInt32(bytes, offset);
        uint value = 20;
        Assert.Equal(value, packed);

        var bytesUint = PackingUtils.LE_To_UInt32(bytes, offset, 1);
        Assert.Equal(value, bytesUint[0]);
        
        uint[] ns = new uint[1];
        PackingUtils.LE_To_UInt32(bytes, offset, ns, 0, 1);
        Assert.Equal(value, ns[0]);
        
        var bytesOut = new byte[8];
        PackingUtils.UInt32_To_LE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = new byte[8];
        PackingUtils.UInt32_To_LE(new uint[] { value }, bytesOutArray, offset);
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffsetArray = new byte[8];
        PackingUtils.UInt32_To_LE(new uint[] { value }, bytesOutOffsetArray, offset);
        Assert.Equal(bytes, bytesOutOffsetArray);
    }
    
    [Fact]
    public void TestLEUInt64Offset()
    {
        // byte array that is equal to 30
        var bytes = new byte[] {  0x00, 0x00, 0x00, 0x1E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var offset = 3;
        var packed = PackingUtils.LE_To_UInt64(bytes, offset);
        ulong value = 30;
        Assert.Equal(value, packed);

        ulong[] ns = new ulong[1];
        PackingUtils.LE_To_UInt64(bytes, offset, ns, 0, 1);
        Assert.Equal(value, ns[0]);
        
        var bytesOut = new byte[16];
        PackingUtils.UInt64_To_LE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = new byte[16];
        PackingUtils.UInt64_To_LE(new ulong[] { value }, bytesOutArray, offset);
        Assert.Equal(bytes, bytesOutArray);
        
        //var bytesOutOffsetArray = new byte[17];
        //PackingUtils.UInt64_To_LE(new ulong[] { value }, bytesOutOffsetArray, offset);
        //Assert.Equal(bytes, bytesOutOffsetArray.Skip(offset).ToArray());
    }

    [Fact]
    public void TestUInt32BEOffset()
    {
        // byte array that is equal to 20
        var offset = 2;
        var bytes = new byte[8];
        PackingUtils.UInt32_To_BE(20, bytes, offset);
        var unpacked = PackingUtils.BE_To_UInt32(bytes, offset);
        uint value = 20;
        Assert.Equal(value, unpacked);
        
        var bytesOut = new byte[8];
        PackingUtils.UInt32_To_BE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = new byte[8];
        PackingUtils.UInt32_To_BE(new uint[] { value }, bytesOutArray, offset);
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffsetArray = new byte[8];
        PackingUtils.UInt32_To_BE(new uint[] { value }, bytesOutOffsetArray, offset);
        Assert.Equal(bytes, bytesOutOffsetArray);
    }
    
    [Fact]
    public void TestUInt64BEOffset()
    {
        // byte array that is equal to 30
        var offset = 3;
        var bytes = new byte[16];
        PackingUtils.UInt64_To_BE(30, bytes, offset);
        var unpacked = PackingUtils.BE_To_UInt64(bytes, offset);
        ulong value = 30;
        Assert.Equal(value, unpacked);
        
        var bytesOut = new byte[16];
        PackingUtils.UInt64_To_BE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = new byte[16];
        PackingUtils.UInt64_To_BE(new ulong[] { value }, bytesOutArray, offset);
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffsetArray = new byte[16];
        PackingUtils.UInt64_To_BE(new ulong[] { value }, bytesOutOffsetArray, offset);
        Assert.Equal(bytes, bytesOutOffsetArray);
    }
    
    [Fact]
    public void TestUInt16LEByte()
    {
        // byte array that is equal to 10
        var offset = 1;
        var bytes = new byte[4];
        PackingUtils.UInt16_To_LE(10, bytes, offset);
        var unpacked = PackingUtils.LE_To_UInt16(bytes, offset);
        ushort value = 10;
        Assert.Equal(value, unpacked);
        
        var bytesOut = new byte[4];
        PackingUtils.UInt16_To_LE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
    }
    
    [Fact]
    public void TestUInt32LEByte()
    {
        // byte array that is equal to 20
        var offset = 2;
        var bytes = new byte[8];
        PackingUtils.UInt32_To_LE(20, bytes, offset);
        var unpacked = PackingUtils.LE_To_UInt32(bytes, offset);
        uint value = 20;
        Assert.Equal(value, unpacked);
        
        var bytesOut = new byte[8];
        PackingUtils.UInt32_To_LE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = new byte[8];
        PackingUtils.UInt32_To_LE(new uint[] { value }, bytesOutArray, offset);
        Assert.Equal(bytes, bytesOutArray);
        
        var bytesOutOffsetArray = new byte[8];
        PackingUtils.UInt32_To_LE(new uint[] { value }, bytesOutOffsetArray, offset);
        Assert.Equal(bytes, bytesOutOffsetArray);
        
    }
    
    [Fact]
    public void TestUInt64LEByte()
    {
        // byte array that is equal to 30
        var offset = 3;
        var bytes = new byte[16];
        PackingUtils.UInt64_To_LE(30, bytes, offset);
        var unpacked = PackingUtils.LE_To_UInt64(bytes, offset);
        ulong value = 30;
        Assert.Equal(value, unpacked);
        
        ulong[] ns = new ulong[] { 30 };
        var bytesValue = new byte[16];
        PackingUtils.UInt64_To_LE(ns, 0, 1, bytesValue, offset);
        Assert.Equal(bytes, bytesValue);
        
        var bytesOut = new byte[16];
        PackingUtils.UInt64_To_LE(value, bytesOut, offset);
        Assert.Equal(bytes, bytesOut);
        
        var bytesOutArray = new byte[16];
        PackingUtils.UInt64_To_LE(new ulong[] { value }, bytesOutArray, offset);
        Assert.Equal(bytes, bytesOutArray);
    }

}
