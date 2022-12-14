using System;
using System.IO;
using System.Numerics;
using System.Text;
using NSubstitute;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Domain;


public class SerializationTests
{
    
    public struct MyType : ISerializable
    {
        public int Value { get; set; }
        public string Name { get; set; }
        
        public MyType(int value, string name)
        {
            Value = value;
            Name = name;
        }
        
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.Write(Value);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Name = reader.ReadVarString();
            Value = reader.ReadInt32();
        }
    }
    
    public struct MyTypeNonSerialize
    {
        public int Value { get; set; }
        public string Name { get; set; }
        
        public MyTypeNonSerialize(int value, string name)
        {
            Value = value;
            Name = name;
        }
        
        public int GetValue(){
            return Value;
        }
        
        public string GetName(){
            return Name;
        }
    }
    
    private enum TestEnum
    {
        Test,
        Test2
    }

    [Fact]
    public void serialize_test()
    {
        var serial = Substitute.For<ISerializable>();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write((byte) 42);
        serial.SerializeData(writer);

        var bArray = stream.ToArray();
        bArray.ShouldBe(new byte[] {42});
    }

    [Fact]
    public void unserialize_test()
    {
        var serial = Substitute.For<ISerializable>();

        using var stream = new MemoryStream(new byte[] {42});
        using var reader = new BinaryReader(stream);

        serial.UnserializeData(reader);
        var readByte = stream.ReadByte();
        readByte.ShouldBe(42);
    }


    
    [Fact]
    public void Test_RegisterType()
    {
        // write the test
        // create a customReader
        // create a customWriter
        // register the customReader and customWriter
        // serialize and unserialize the object
        // check if the object is the same as the original
        var expected = new MyType(42, "test");
        
        var customReader = new CustomReader(reader =>
        {
            var obj = new MyType();
            obj.UnserializeData(reader);
            return obj;
        });
        
        var customWriter = new CustomWriter((writer, obj) => 
        {
            (obj as ISerializable).SerializeData(writer);
        });
        
        Serialization.RegisterType<MyType>(customReader, customWriter);

        var bytes = new byte[1024];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        Serialization.Serialize(writer, expected);
        
        var streamRead = new MemoryStream(bytes);
        var reader = new BinaryReader(streamRead);
        
        var actual = Serialization.Unserialize<MyType>(reader);
        
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Value, actual.Value);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Null()
    {
        // Arrange
        var input = null as object;

        // Act
        var output = Serialization.Serialize(input);
        var unserialize = Serialization.Unserialize<object>(output);

        // Assert
        Assert.Equal(new byte[0], output);
        Assert.Equal(input, unserialize);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_SomeTypeNull()
    {
        // Arrange
        int? input = null;
        var bytes = new byte[4];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        
        // Act
        var output = Serialization.Serialize(input);
        var unserialize = Serialization.Unserialize(output, typeof(int));

        // Assert
        Assert.Equal(new byte[0], output);
        Assert.Equal(input, unserialize);
    }

    [Fact]
    public void Test_Serialize_Method_With_Void(){
        // Arrange
        int input = 5;
        var bytes = new byte[4];
        var stream = new MemoryStream(bytes);
        var writer = new BinaryWriter(stream);
        var reader = new BinaryReader(stream);
        
        // Act
        Serialization.Serialize(writer, input, typeof(void));
        Assert.Equal(new byte[4], bytes);

        Assert.Throws<NotSupportedException>(() => Serialization.Unserialize(reader, typeof(void)));
    }

    
    [Fact]
    public void Test_Serialize_Method_With_Byte_Array()
    {
        // Arrange
        var input = new byte[] { 1, 2, 3, 4 };

        // Act
        var output = Serialization.Serialize(input);

        // Assert
        Assert.Equal(input, output);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Boolean()
    {
        // Arrange
        var input = true;

        // Act
        var output = Serialization.Serialize(input);

        // Assert
        Assert.Equal(new byte[] { 1 }, output);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Byte()
    {
        // Arrange
        var input = (byte) 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialize = Serialization.Unserialize<byte>(output);

        // Assert
        Assert.Equal(new byte[] { 1 }, output);
        Assert.Equal(input, unserialize);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_SByte()
    {
        // Arrange
        var input = (sbyte) 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialize = Serialization.Unserialize<sbyte>(output);

        // Assert
        Assert.Equal(new byte[] { 1 }, output);
        Assert.Equal(input, unserialize);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Int16()
    {
        // Arrange
        var input = (short) 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<short>(output);

        // Assert
        Assert.Equal(new byte[] { 1, 0 }, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_UInt16()
    {
        // Arrange
        var input = (ushort) 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<ushort>(output);

        // Assert
        Assert.Equal(new byte[] { 1, 0 }, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Int32()
    {
        // Arrange
        var input = 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<int>(output);

        // Assert
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_UInt32()
    {
        // Arrange
        var input = (uint) 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<uint>(output);

        // Assert
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Int64()
    {
        // Arrange
        var input = (long) 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<long>(output);

        // Assert
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, output);
        Assert.Equal(input, unserialized);
    }

    [Fact]
    public void Test_Serialize_Method_With_UInt64()
    {
        // Arrange
        var input = (ulong) 1;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<ulong>(output);

        // Assert
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_String()
    {
        // Arrange
        var input = "test";
        var expected = new byte[]{4, 116, 101, 115, 116};

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<string>(output);

        // Assert
        Assert.Equal(expected, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_ByteArray()
    {
        // Arrange
        var input = new byte[] { 1, 2, 3, 4 };
        var expected = new byte[] {  1, 2, 3, 4 };

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<byte[]>(output);

        // Assert
        Assert.Equal(expected, output);
        Assert.Equal(expected, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_ByteArray_Null()
    {
        // Arrange
        byte[] input = null;

        // Act
        var output = Serialization.Serialize(input);

        // Assert
        Assert.Equal(new byte[0], output);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Decimal()
    {
        // Arrange
        var input = 1.1m;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<decimal>(output);

        // Assert
        Assert.Equal(new byte[] { 11, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 }, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_BigInteger()
    {
        // Arrange
        var input = new BigInteger(100);

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<BigInteger>(output);

        // Assert
        Assert.Equal(new byte[] { 2, 100, 0 }, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_Timestamp()
    {
        // Arrange
        var input = new Timestamp(1);

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<Timestamp>(output);

        // Assert
        Assert.Equal(new byte[] { 1, 0, 0, 0}, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_IntArray()
    {
        // Arrange
        var input = new int[] { 1, 2, 3, 4 };
        var expected = new byte[] { 4, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0 };

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<int[]>(output);

        // Assert
        Assert.Equal(expected, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_Serialize_Method_With_IntArray_Null()
    {
        // Arrange
        int[] input = null;

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<int[]>(output);

        // Assert
        Assert.Equal(new byte[0], output);
        Assert.Equal(input, unserialized);
    }

    [Fact]
    public void Test_Serialize_Method_With_ISerializable()
    {
        // Arrange
        var input = new MyType(10, "test");
        var expected = new byte[] { 4, 116, 101, 115, 116, 10, 0, 0, 0 };

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<MyType>(output);

        // Assert
        Assert.Equal(expected, output);
        Assert.Equal(input.Name, unserialized.Name);
        Assert.Equal(input.Value, unserialized.Value);
        Assert.Equal(input.ToString(), unserialized.ToString());
        Assert.Equal(input.Serialize(), unserialized.Serialize());
    }


    [Fact]
    public void Test_Serialize_Method_With_Enum()
    {
        // Arrange
        var input = TestEnum.Test;
        var expected = new byte[] { 0 };

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<TestEnum>(output);

        // Assert
        Assert.Equal(expected, output);
        Assert.Equal(input, unserialized);
    }

    [Fact]
    public void Test_Serialize_Method_With_Struct()
    {
        // Arrange
        var input = new MyTypeNonSerialize(1, "test");
        var expected = new byte[] { 1, 0, 0, 0, 4, 116, 101, 115, 116 };

        // Act
        var output = Serialization.Serialize(input);
        var unserialized = Serialization.Unserialize<MyTypeNonSerialize>(output);

        // Assert
        Assert.Equal(expected, output);
        Assert.Equal(input, unserialized);
    }
    
    [Fact]
    public void Test_AsSerializable()
    {
        // Arrange
        var input = new MyType(10, "test");
        var expected = new byte[] { 4, 116, 101, 115, 116, 10, 0, 0, 0 };

        // Act
        var output = (MyType)Serialization.AsSerializable(expected, typeof(MyType));

        // Assert
        Assert.Equal(input, output);
        Assert.Equal(input.Name, output.Name);
        Assert.Equal(input.Value, output.Value);
    }



}
