using System.IO;

namespace Phantasma.Core.Tests.Utils;

using Xunit;

using Phantasma.Core.Utils;

public class StreamUtilsTests
{
    // Create tests for StreamExtensions class methods
    
    [Fact]
    public void TestsReadUInt24()
    {
        var bytes = new byte[64];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);

        var streamReader = new MemoryStream(bytes);
        var reader = new BinaryReader(streamReader);
        uint value = 10;
        writer.WriteUInt24(value);

        uint result = reader.ReadUInt24();
        
        Assert.Equal(value, result);
    }
    
    [Fact]
    public void TestsReadUInt32()
    {
        var bytes = new byte[64];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);

        var streamReader = new MemoryStream(bytes);
        var reader = new BinaryReader(streamReader);
        uint value = 10;
        writer.Write(value);

        uint result = reader.ReadUInt32();
        
        Assert.Equal(value, result);
    }
    
    [Fact]
    public void TestsReadUInt64()
    {
        var bytes = new byte[64];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);

        var streamReader = new MemoryStream(bytes);
        var reader = new BinaryReader(streamReader);
        ulong value = 10;
        writer.Write(value);

        ulong result = reader.ReadUInt64();
        
        Assert.Equal(value, result);
    }
    
    [Fact]
    public void TestsReadVarInt()
    {
        var bytes = new byte[64];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);

        var streamReader = new MemoryStream(bytes);
        var reader = new BinaryReader(streamReader);
        ulong value = 10;
        writer.WriteVarInt((int)value);

        ulong result = reader.ReadVarInt();
        
        Assert.Equal(value, result);
    }
}
