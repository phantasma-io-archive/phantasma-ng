using System;
using System.Numerics;
using Phantasma.Core.Numerics;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Numerics;

[Collection("Numerics")]
public class BigIntegerExtensionTests
{
    [Fact]
    public void test_mod()
    {
        BigInteger value = 11;
        const int divide = 3;
        var modResult = value.Mod(divide);
        modResult.ShouldBe(2);
    }

    [Fact]
    public void test_bit_true()
    {
        BigInteger value = 11;
        var bitResult = value.TestBit(3);
        bitResult.ShouldBeTrue();
    }

    [Fact]
    public void test_bit_false()
    {
        BigInteger value = 11;
        var bitResult = value.TestBit(2);
        bitResult.ShouldBeFalse();
    }

    [Fact]
    public void get_lowest_set_bit()
    {
        BigInteger value = 32;
        var result = value.GetLowestSetBit();
        result.ShouldBe(5);
    }

    [Fact]
    public void get_bit_length()
    {
        BigInteger value = 31;
        var result = value.GetBitLength();
        result.ShouldBe(5);
    }

    [Fact]
    public void hex_to_big_integer()
    {
        const string value = "2a";
        var result = value.HexToBigInteger();
        result.ShouldBe(42);
    }

    [Fact]
    public void mod_inverse()
    {
        BigInteger value = 3;
        BigInteger divide = 11;
        var modResult = value.ModInverse(divide);
        modResult.ShouldBe(4);
    }
    
    [Fact]
    public void TestModInverse_VLessThanZero()
    {
        // define the inputs to the ModInverse method
        BigInteger a = 7;
        BigInteger n = 11;

        // compute the expected result
        BigInteger expected = 8;

        // compute the actual result
        BigInteger actual = a.ModInverse(n);

        // check if the actual result is less than 0
        Assert.Equal(expected, actual);

        // check if the actual result equals the expected result after applying the if statement
        Assert.Equal(expected, (actual + n) % n);
    }

    [Fact]
    public void is_parsable_true()
    {
        const string value = "1234";
        var result = value.IsParsable();
        result.ShouldBeTrue();
    }

    [Fact]
    public void is_parsable_false()
    {
        const string value = "2g";
        var result = value.IsParsable();
        result.ShouldBeFalse();
        
        const string value3 = null;
        var result3 = value3.IsParsable();
        result3.ShouldBeFalse();
    }
    
    [Fact]
    public void test_getBitLength()
    {
        BigInteger value = 31;
        var result = BigIntegerExtension.GetBitLength(value);
        result.ShouldBe(5);
        
        var result2 = BigIntegerExtension.GetBitLength(value);
        result2.ShouldBe(5);
    }
    
    [Fact]
    public void test_getLowestSetBit()
    {
        BigInteger value = 32;
        var result = BigIntegerExtension.GetLowestSetBit(value);
        result.ShouldBe(5);
        
        value = 0;
        result = BigIntegerExtension.GetLowestSetBit(value);
        result.ShouldBe(-1);
        
        value = new BigInteger(255);
        result = BigIntegerExtension.GetLowestSetBit(value);
        result.ShouldBe(0);
        
        value = new BigInteger(new byte[]{0x00, 0x00, 0xFF, 0x00});
        result = BigIntegerExtension.GetLowestSetBit(value);
        result.ShouldBe(16);
        
        // Test throw exception
        //value = new BigInteger(new byte[]{0x00, 0x00, 0x00, 0x00});
        //Should.Throw<Exception>(() => BigIntegerExtension.GetLowestSetBit(value));

    }
    
    [Fact]
    public void test_toUsignedByteArray()
    {
        BigInteger value = 32;
        var result = BigIntegerExtension.ToUnsignedByteArray(value);
        result.ShouldBe(new byte[] {32});
        
        value = BigInteger.Zero;
        result =  BigIntegerExtension.ToUnsignedByteArray(value);
        result.ShouldBe( new byte[0]);
        
        value = new BigInteger(-10);
        result = BigIntegerExtension.ToUnsignedByteArray(value);
        result.ShouldBe(new byte[] {10});
        
        value = new BigInteger(new byte[]{0x00, 0x01, 0x01, 0x00, 0xFF, 0x00});
        result = BigIntegerExtension.ToUnsignedByteArray(value);
        result.ShouldBe(new byte[] {0, 1, 1, 0, 255});
        
        value = new BigInteger(new byte[]{0xff, 0x00, 0x00});
        result = BigIntegerExtension.ToUnsignedByteArray(value);
        result.ShouldBe(new byte[] {255});
        
        value = new BigInteger(-255);
        result = BigIntegerExtension.ToUnsignedByteArray(value);
        result.ShouldBe( new byte[] { 255 });
        
    }
    
    [Fact]
    public void test_toSignedByteArray()
    {
        BigInteger value = 32;
        var result = value.ToSignedByteArray();
        result.ShouldBe(new byte[] {32, 0});
        
        value = BigInteger.Zero;
        result = value.ToSignedByteArray();
        result.ShouldBe( new byte[]{0x00});
        
        value = new BigInteger(-10);
        result = value.ToSignedByteArray();
        result.ShouldBe(new byte[] {256-10, 0xff, 0xff});
        
        value = new BigInteger(-255);
        result = value.ToSignedByteArray();
        result.ShouldBe(new byte[] {1, 0xff, 0xff});
        
        value = new BigInteger(255);
        result = value.ToSignedByteArray();
        result.ShouldBe(new byte[] {255, 0});
    }
    
    [Fact]
    public void byte_array_to_big_integer()
    {
        var byteArray = new byte[] {42};
        var bigInteger = byteArray.AsBigInteger();
        bigInteger.ShouldBe(42);
    }

    [Fact]
    public void big_integer_to_byte_array()
    {
        BigInteger bigInteger = 42;
        var byteArray = bigInteger.AsByteArray();
        byteArray.ShouldBe(new byte[] {42});
    }
}
