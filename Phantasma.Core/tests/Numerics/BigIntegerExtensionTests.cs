using System.Numerics;
using Phantasma.Core.Numerics;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests;

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
    }
}
