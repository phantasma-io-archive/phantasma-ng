using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class VMObjectTests
{
    [Fact]
    public void TestConstructorVMObject()
    {
        var vmObject = new VMObject();
        Assert.NotNull(vmObject);
        Assert.Equal(VMType.None, vmObject.Type);
        Assert.Equal(null, vmObject.Data);
    }
    
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
        var data = true;
        
        var myClass = new VMObject();
        myClass.SetDefaultValue(VMType.Bool);
        myClass.SetValue(data);
        
        // Act
        var result = myClass.AsByteArray();

        // Assert
        Assert.Equal(new byte[] { 0x01 }, result);
    }
    
    [Fact]
    public void TestAsNumber()
    {
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Number);
        vmObject.SetValue(new BigInteger(12345));

        var result = vmObject.AsNumber();

        Assert.Equal(12345, result);
    }
    
    [Fact]
    public void TestAsTimestamp()
    {
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Timestamp);
        vmObject.SetValue(new Timestamp(12345));
        

        var result = vmObject.AsTimestamp();

        Assert.Equal(new Timestamp(12345), result);
    }
    
    [Fact]
    public void TestAsDateTime()
    {
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Timestamp);
        vmObject.SetValue((DateTime)new Timestamp(12345));
        

        var result = vmObject.AsTimestamp();

        Assert.Equal(new Timestamp(12345), result);
    }

    [Fact]
    public void TestAsType()
    {
        var expected = new BigInteger(12345);
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Number);
        vmObject.SetValue(expected);

        var result = vmObject.AsType(VMType.Number);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestAsEnum()
    {
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Enum);
        vmObject.SetValue(TestEnum.TestEnumValue);

        var result = vmObject.AsEnum<TestEnum>();

        Assert.Equal(TestEnum.TestEnumValue, result);
    }

    [Fact]
    public void TestAsBool()
    {
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Bool);
        vmObject.SetValue(true);
        
        var result = vmObject.AsBool();

        Assert.True(result);
    }

    [Fact]
    public void TestAsByteArray()
    {
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Bytes);
        vmObject.SetValue(new byte[] { 0x01, 0x02, 0x03 });
        
        var result = vmObject.AsByteArray();

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, result);
    }

    [Fact]
    public void TestSize()
    {
        var obj1 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(123));
        var obj2 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(456));
        var obj3 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(789));
        var obj4 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(101112));
        var dictObject = new Dictionary<VMObject, VMObject>
        {
            { obj1, obj2 },
            { obj3, obj4 }
        };
        
        var vmObject = new VMObject();
        vmObject.SetDefaultValue(VMType.Object);
        vmObject.SetValue(dictObject);

        var result = vmObject.Size;

        Assert.Equal(4, result);
    }

    [Fact]
    public void TestAsArray()
    {
        var obj1 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(0));
        var obj2 = new VMObject().SetDefaultValue(VMType.String).SetValue("tests my string");
        var obj3 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(1));
        var obj4 = new VMObject().SetDefaultValue(VMType.String).SetValue("test it as you can");
        var dictObject = new Dictionary<VMObject, VMObject>
        {
            { obj1, obj2 },
            { obj3, obj4 }
        };
        
        var vmObject = new VMObject();
        vmObject.SetValue(dictObject);
        
        var result = vmObject.AsArray(VMType.Struct);
        
        Assert.Equal(obj2.AsString(), result[0].AsString());
        Assert.Equal(obj4.AsString(), result[1].AsString());
    }
    
    [Fact] 
    public void TestAsArrayNull()
    {
        var obj1 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(0));
        var obj2 = new VMObject().SetDefaultValue(VMType.String).SetValue("tests my string");
        var obj3 = new VMObject().SetDefaultValue(VMType.Number).SetValue((int)new BigInteger(1));
        var obj4 = new VMObject().SetDefaultValue(VMType.String).SetValue("test it as you can");
        var dictObject = new Dictionary<VMObject, VMObject>();
        var vmObject = new VMObject();
        vmObject.SetValue(dictObject);
        
        // Just for the error 
        vmObject.SetDefaultValue(VMType.Number);
        Assert.Throws<Exception>(() => vmObject.AsArray(VMType.Number));
        
        // real values
        vmObject.SetValue(dictObject);
        var result = vmObject.AsArray(VMType.Struct);
        Assert.Equal(new VMObject[0], result);
        
        
    }
    
    [Fact]
    public void TestAsNull()
    {
        var listObjs = new BigInteger[] { 1, 2, 3, 4, 5 };
        var vmObject = new VMObject();
        Assert.Null(vmObject.Data);
        Assert.Throws<Exception>(() => vmObject.AsArray(VMType.Struct));
    }


    [Fact]
    public void TestIsVMType()
    {
        Assert.True(VMObject.IsVMType(typeof(TestEnum)));
        Assert.True(VMObject.IsVMType(typeof(BigInteger)));
        

    }
    
    public enum TestEnum
    {
        TestEnumValue
    }

}
