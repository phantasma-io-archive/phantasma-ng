using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Phantasma.Core.Storage;
using Phantasma.Core.Utils;

namespace Phantasma.Core.Tests.Utils;

using Xunit;

public class IOUtilsTests
{
    // write tests based on the IOUtils class methods
    [Fact]
    public void TestReadBytes()
    {
        var bytes = new byte[4] { 0x13, 0x04, 0x05, 0x02 };
        var stream = new MemoryStream(bytes);
        var reader = new BinaryReader(stream);
        var result = IOUtils.ReadFixedBytes(reader, 4);
        Assert.Equal(4, result.Length);
        Assert.Equal(0x13, result[0]);
        Assert.Equal(0x04, result[1]);
        Assert.Equal(0x05, result[2]);
        Assert.Equal(0x02, result[3]);
    }
    
    [Fact]
    public void TestReadVarInt()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var stream = new MemoryStream(bytes);
        var reader = new BinaryReader(stream);
        var result = IOUtils.GetVarSize(10);
        Assert.Equal(0x01, result);
    }
    
    [Fact]
    public void TestWriteVarInt()
    {
        var bytes = new byte[8];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        IOUtils.WriteVarInt(writer, 10);
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadVarInt(reader);
        ulong value = 10; 
        Assert.Equal(value, result);
    }
    
    [Fact]
    public void TestWriteBigInteger()
    {
        var bytes = new byte[8];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        IOUtils.WriteBigInteger(writer, 100);
        
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadBigInteger(reader);
        Assert.Equal( 100, result);
    }
    
    [Fact]
    public void TestWriteByteArray()
    {
        var bytes = new byte[8];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        IOUtils.WriteByteArray(writer, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
        
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadByteArray(reader);
        Assert.Equal( 5, result.Length);
        Assert.Equal( 0x01, result[0]);
        Assert.Equal( 0x02, result[1]);
        Assert.Equal( 0x03, result[2]);
        Assert.Equal( 0x04, result[3]);
        Assert.Equal( 0x05, result[4]);
    }
    
    [Fact]
    public void TestWriteVarString()
    {
        var bytes = new byte[32];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        IOUtils.WriteVarString(writer, "Hello World");
        
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadVarString(reader);
        Assert.Equal( "Hello World", result);
    }
    
    [Fact]
    public void TestWriteVarBytes()
    {
        var bytes = new byte[32];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        IOUtils.WriteVarBytes(writer, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
        
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadVarBytes(reader);
        Assert.Equal( 5, result.Length);
        Assert.Equal( 0x01, result[0]);
        Assert.Equal( 0x02, result[1]);
        Assert.Equal( 0x03, result[2]);
        Assert.Equal( 0x04, result[3]);
        Assert.Equal( 0x05, result[4]);
    }
    
    public class TestStruct : Core.Domain.ISerializable
    {
        public int a;
        public string b;

        public int Size { get; }
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarInt(a);
            writer.WriteVarString(b);
        }

        public void UnserializeData(BinaryReader reader)
        {
            a = reader.Read();
            b = reader.ReadString();
        }
    }
    
    [Fact]
    public void TestWrite()
    {
        var bytes = new byte[64];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        var myStruct = new TestStruct[] { new TestStruct
        {
            a = 10,
            b = "Hello World"
        }};
        // create a IReadOnlyCollection<byte>
        IReadOnlyCollection<TestStruct> myCollection = myStruct;
        IOUtils.Write(writer, myCollection);
        
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadSerializableArray<TestStruct>(reader, 1);
        Assert.Equal( 1, result.Length);
        Assert.Equal( 10, result[0].a);
        Assert.Equal( "Hello World", result[0].b);
    }
    
    [Fact]
    public void TestWriteSerializable()
    {
        var bytes = new byte[128];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        var myStruct = new TestStruct
        {
            a = 10,
            b = "Hello World"
        };
        IOUtils.Write(writer, myStruct);
        
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadSerializable<TestStruct>(reader);
        Assert.Equal( 10, result.a);
        Assert.Equal( "Hello World", result.b);
    }
    
    [Fact]
    public void TestWriteNullableArray()
    {
        /*var bytes = new byte[256];
        // create a variable type of BinaryWriter
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        var myStruct = new TestStruct[] { new TestStruct
        {
            a = 10,
            b = "Hello World"
        }};
        // create a IReadOnlyCollection<byte>
        IOUtils.WriteNullableArray<TestStruct>(writer, myStruct);
        
        
        var stream2 = new MemoryStream(bytes);
        var reader = new BinaryReader(stream2);
        var result = IOUtils.ReadSerializableArray<TestStruct>(reader, 1);
        Assert.Equal( 1, result.Length);
        Assert.Equal( 10, result[0].a);
        Assert.Equal( "Hello World", result[0].b);*/
    }
}
