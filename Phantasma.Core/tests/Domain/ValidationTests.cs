using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Domain.VM.Enums;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class ValidationTests
{
    [Fact]
    public void is_reserved_test_true()
    {
        ValidationUtils.IsReservedIdentifier("phantasma").ShouldBeTrue();
    }

    [Fact]
    public void is_reserved_test_false()
    {
        ValidationUtils.IsReservedIdentifier("herbert").ShouldBeFalse();
    }

    [Fact]
    public void is_valid_identifier_valid()
    {
        ValidationUtils.IsValidIdentifier("abc").ShouldBeTrue();
    }

    [Fact]
    public void is_valid_identifier_valid_with_numbers_and_underscore()
    {
        ValidationUtils.IsValidIdentifier("herbert_0815").ShouldBeTrue();
    }

    [Fact]
    public void is_valid_identifier_not_valid()
    {
        ValidationUtils.IsValidIdentifier("Herbert").ShouldBeFalse();
    }

    [Fact]
    public void is_valid_identifier_not_valid_special_char()
    {
        ValidationUtils.IsValidIdentifier("herbert-0815").ShouldBeFalse();
    }


    [Fact]
    public void is_valid_ticker_valid()
    {
        ValidationUtils.IsValidTicker("ABC").ShouldBeTrue();
    }

    [Fact]
    public void is_valid_ticker_valid_again()
    {
        ValidationUtils.IsValidTicker("XYZ").ShouldBeTrue();
    }

    [Fact]
    public void is_valid_ticker_not_valid()
    {
        ValidationUtils.IsValidTicker("abc").ShouldBeFalse();
    }

    [Fact]
    public void is_valid_ticker_not_valid_again()
    {
        ValidationUtils.IsValidTicker("ABC-").ShouldBeFalse();
    }

    [Fact]
    public void is_valid_ticker_genesis()
    {
        ValidationUtils.IsValidTicker(ValidationUtils.GENESIS_NAME).ShouldBeFalse();
    }

    [Fact]
    public void is_valid_method_bool_true()
    {
        ValidationUtils.IsValidMethod("isTrue", VMType.Bool).ShouldBeTrue();
    }

    [Fact]
    public void is_valid_method_bool_false()
    {
        ValidationUtils.IsValidMethod("is", VMType.Bool).ShouldBeFalse();
    }

    [Fact]
    public void is_valid_method_none_true()
    {
        ValidationUtils.IsValidMethod("onHerbert", VMType.None).ShouldBeTrue();
    }

    [Fact]
    public void is_valid_method_none_false()
    {
        ValidationUtils.IsValidMethod("onHerbert", VMType.Bool).ShouldBeFalse();
    }

    [Fact]
    public void is_valid_method_get_true()
    {
        ValidationUtils.IsValidMethod("getNumber", VMType.Number).ShouldBeTrue();
    }

    [Fact]
    public void is_valid_method_get_false()
    {
        ValidationUtils.IsValidMethod("getNumber", VMType.None).ShouldBeFalse();
    }

    [Fact]
    public void is_valid_method_testMethod()
    {
        Assert.True(ValidationUtils.IsValidMethod("testMethod", VMType.Bool));
    }
    
    [Fact]
    public void is_valid_identifier_null()
    {
        Assert.False(ValidationUtils.IsValidIdentifier(null));
    }
    
    [Fact]
    public void is_valid_identifier_anon_genesis_entry()
    {
        Assert.False(ValidationUtils.IsValidIdentifier(ValidationUtils.ANONYMOUS_NAME));
    }
    
    [Fact]
    public void is_valid_identifier_less_3()
    {
        Assert.False(ValidationUtils.IsValidIdentifier("ab"));
    }
    
    [Fact]
    public void is_valid_ticker_null()
    {
        Assert.False(ValidationUtils.IsValidTicker(null));
    }
    
    [Fact]
    public void is_reserved_identifier_infusionName()
    {
        Assert.True(ValidationUtils.IsReservedIdentifier(DomainSettings.InfusionName));
    }
    
    [Fact]
    public void is_reserved_identifier_null()
    {
        Assert.True(ValidationUtils.IsReservedIdentifier("null"));
    }
    
    [Fact]
    public void is_reserved_identifier_huawei()
    {
        Assert.True(ValidationUtils.IsReservedIdentifier("huawei"));
    }
}
