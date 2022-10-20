using Phantasma.Core.Numerics;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests;

public class UnitConversionTests
{
    [Fact]
    public void to_big_integer()
    {
        const int units = 10;
        const int dec = 100000000;
        var multiplier = UnitConversion.ToBigInteger(dec, units);
        multiplier.ShouldBe(1000000000000000000);
    }

    [Fact]
    public void to_decimal()
    {
        const long bigInteger = 1000000000000000000;
        const int units = 10;
        var dec = UnitConversion.ToDecimal(bigInteger, units);
        dec.ShouldBe(100000000);
    }

    [Fact]
    public void string_to_decimal()
    {
        const string bigIntegerString = "1000000000000000000";
        const int units = 10;
        var dec = UnitConversion.ToDecimal(bigIntegerString, units);
        dec.ShouldBe(100000000);
    }

    [Fact]
    public void convert_decimals()
    {
        const int value = 100000000;
        const int fromDecForm = 8;
        const int toDecForm = 10;
        var bigInteger = UnitConversion.ConvertDecimals(value, fromDecForm, toDecForm);
        bigInteger.ShouldBe(10000000000);
    }

    [Fact]
    public void get_unit_value()
    {
        const int dec = 10;
        const long value = 10000000000;
        var bigInteger = UnitConversion.GetUnitValue(dec);
        bigInteger.ShouldBe(value);
    }
}
