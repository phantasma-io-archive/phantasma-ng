using Phantasma.Core.Numerics;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Numerics;

[Collection("Numerics")]
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
    public void to_big_integer_value_0()
    {
        const int units = 10;
        const int dec = 0;
        var multiplier = UnitConversion.ToBigInteger(dec, units);
        multiplier.ShouldBe(0);
    }
    
    [Fact]
    public void to_big_integer_unit_0()
    {
        const int units = 0;
        const int dec = 1000;
        var multiplier = UnitConversion.ToBigInteger(dec, units);
        multiplier.ShouldBe(1000);
    }

    [Fact]
    public void to_big_integer_decimal_places_values()
    {
        const int units = 2;
        const decimal dec = 1000.50m;
        var multiplier = UnitConversion.ToBigInteger(dec, units);
        multiplier.ShouldBe(100050);
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
    public void to_decimal_value_0()
    {
        const long bigInteger = 0;
        const int units = 10;
        var dec = UnitConversion.ToDecimal(bigInteger, units);
        dec.ShouldBe(0);
    }
    
    [Fact]
    public void to_decimal_unit_0()
    {
        const long bigInteger = 10;
        const int units = 0;
        var dec = UnitConversion.ToDecimal(bigInteger, units);
        dec.ShouldBe(10);
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
    public void string_to_decimal_null(){
        const string bigIntegerString = null;
        const int units = 10;
        var dec = UnitConversion.ToDecimal(bigIntegerString, units);
        dec.ShouldBe(0);
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
    public void convert_decimals_value_0()
    {
        const int value = 0;
        const int fromDecForm = 8;
        const int toDecForm = 10;
        var bigInteger = UnitConversion.ConvertDecimals(value, fromDecForm, toDecForm);
        bigInteger.ShouldBe(0);
    }
    
    [Fact]
    public void convert_decimals_same_form()
    {
        const int value = 100000000;
        const int fromDecForm = 8;
        const int toDecForm = 8;
        var bigInteger = UnitConversion.ConvertDecimals(value, fromDecForm, toDecForm);
        bigInteger.ShouldBe(100000000);
    }
    
    [Fact]
    public void convert_decimals_from_dec_form_0()
    {
        const int value = 100000000;
        const int fromDecForm = 0;
        const int toDecForm = 8;
        var bigInteger = UnitConversion.ConvertDecimals(value, fromDecForm, toDecForm);
        bigInteger.ShouldBe(10000000000000000);
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
