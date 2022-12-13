using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Utils;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class VMObjectTests
{
    
    [Fact]
    public void UnserializeData_SetsExpectedProperties_ForBoolType()
    {
        // Arrange
        var data = true;
        var expectedType = VMType.Bool;
        var expectedData = data;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        Serialization.Serialize(writer, expectedData);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);
    }
    
    [Fact]
    public void UnserializeData_SetsExpectedProperties_ForBytesType()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var expectedType = VMType.Bytes;
        var expectedData = data;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        Serialization.Serialize(writer, expectedData);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);
    }
    
    [Fact (Skip = "Not implemented")]
    public void UnserializeData_SetsExpectedProperties_ForNumberType()
    {
        // Arrange
        var data = 1234567890;
        var expectedType = VMType.Number;
        var expectedData = data;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        Serialization.Serialize(writer, expectedData);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);
    }
    
    [Fact]
    public void UnserializeData_SetsExpectedProperties_ForStringType()
    {
        // Arrange
        var data = "hello world";
        var expectedType = VMType.String;
        var expectedData = data;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        Serialization.Serialize(writer, expectedData);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);
    }
    
    [Fact]
    public void UnserializeData_SetsExpectedProperties_ForStructType()
    {
        // Arrange
        /*var data = new Dictionary<VMObject, VMObject>
        {
            { new VMObject(VMType.String, "key1"), new VMObject(VMType.Number, 1234567890) },
            { new VMObject(VMType.String, "key2"), new VMObject(VMType.Number, 9876543210) }
        };
        var expectedType = VMType.Struct;
        var expectedData = data;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        writer.WriteVarInt(data.Count);
        foreach (var kvp in data)
        {
            kvp.Key.SerializeData(writer);
            kvp.Value.SerializeData(writer);
        }
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);*/
    }
    
    [Fact (Skip = "Not implemented")]
    public void UnserializeData_SetsExpectedProperties_ForObjectType()
    {
        // Arrange
        var data = PhantasmaKeys.Generate().Address;
        var expectedType = VMType.Object;
        var expectedData = data;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        Serialization.Serialize(writer, data);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);
    }
    
    [Fact]
    public void UnserializeData_SetsExpectedProperties_ForEnumType()
    {
        // Arrange
        var data = (uint)1234567890;
        var expectedType = VMType.Enum;
        var expectedData = data;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        writer.WriteVarInt(data);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);
    }
    
    [Fact]
    public void UnserializeData_SetsExpectedProperties_ForNoneType()
    {
        // Arrange
        var expectedType = VMType.None;
        var expectedData = null as object;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act
        myClass.UnserializeData(reader);

        // Assert
        Assert.Equal(expectedType, myClass.Type);
        Assert.Equal(expectedData, myClass.Data);
    }

    [Fact]
    public void UnserializeData_ThrowsException_ForInvalidType()
    {
        // Arrange
        var expectedType = (VMType)255;

        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream);
        writer.Write((byte)expectedType);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);

        var myClass = new VMObject();

        // Act and assert
        var ex = Assert.Throws<Exception>(() => myClass.UnserializeData(reader));
        Assert.Equal($"invalid unserialize: type {expectedType}", ex.Message);
    }

    
    [Fact]
    public void AsByteArray_ReturnsExpectedResult_ForBytesType()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var myClass = new VMObject().SetValue(data);

        // Act
        var result = myClass.AsByteArray();

        // Assert
        Assert.Equal(data, result);
    }

    [Fact]
    public void AsByteArray_ReturnsExpectedResult_ForBoolType()
    {
        // Arrange
        /*var data = true;
        
        var myClass = new VMObject();
        myClass.Type = VMType.Bool;
        myClass.Data = data;
        
        // Act
        var result = myClass.AsByteArray();

        // Assert
        Assert.Equal(new byte[] { 0x01 }, result);*/
    }
}
