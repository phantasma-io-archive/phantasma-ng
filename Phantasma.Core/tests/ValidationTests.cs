using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Core.Domain;
using Shouldly;

namespace Phantasma.Core.Tests;

[TestClass]
public class ValidationTests
{
    [TestMethod]
    public void is_reserved_test_true()
    {
        ValidationUtils.IsReservedIdentifier("phantasma").ShouldBeTrue();
    }

    [TestMethod]
    public void is_reserved_test_false()
    {
        ValidationUtils.IsReservedIdentifier("herbert").ShouldBeFalse();
    }

    [TestMethod]
    public void is_valid_identifier_valid()
    {
        ValidationUtils.IsValidIdentifier("abc").ShouldBeTrue();
    }

    [TestMethod]
    public void is_valid_identifier_valid_with_numbers_and_underscore()
    {
        ValidationUtils.IsValidIdentifier("herbert_0815").ShouldBeTrue();
    }

    [TestMethod]
    public void is_valid_identifier_not_valid()
    {
        ValidationUtils.IsValidIdentifier("Herbert").ShouldBeFalse();
    }

    [TestMethod]
    public void is_valid_identifier_not_valid_special_char()
    {
        ValidationUtils.IsValidIdentifier("herbert-0815").ShouldBeFalse();
    }


    [TestMethod]
    public void is_valid_ticker_valid()
    {
        ValidationUtils.IsValidTicker("ABC").ShouldBeTrue();
    }

    [TestMethod]
    public void is_valid_ticker_valid_again()
    {
        ValidationUtils.IsValidTicker("XYZ").ShouldBeTrue();
    }

    [TestMethod]
    public void is_valid_ticker_not_valid()
    {
        ValidationUtils.IsValidTicker("abc").ShouldBeFalse();
    }

    [TestMethod]
    public void is_valid_ticker_not_valid_again()
    {
        ValidationUtils.IsValidTicker("ABC-").ShouldBeFalse();
    }

    [TestMethod]
    public void is_valid_ticker_genesis()
    {
        ValidationUtils.IsValidTicker(ValidationUtils.GENESIS_NAME).ShouldBeFalse();
    }

    [TestMethod]
    public void is_valid_method_bool_true()
    {
        ValidationUtils.IsValidMethod("isTrue", VMType.Bool).ShouldBeTrue();
    }

    [TestMethod]
    public void is_valid_method_bool_false()
    {
        ValidationUtils.IsValidMethod("is", VMType.Bool).ShouldBeFalse();
    }

    [TestMethod]
    public void is_valid_method_none_true()
    {
        ValidationUtils.IsValidMethod("onHerbert", VMType.None).ShouldBeTrue();
    }

    [TestMethod]
    public void is_valid_method_none_false()
    {
        ValidationUtils.IsValidMethod("onHerbert", VMType.Bool).ShouldBeFalse();
    }

    [TestMethod]
    public void is_valid_method_get_true()
    {
        ValidationUtils.IsValidMethod("getNumber", VMType.Number).ShouldBeTrue();
    }

    [TestMethod]
    public void is_valid_method_get_false()
    {
        ValidationUtils.IsValidMethod("getNumber", VMType.None).ShouldBeFalse();
    }
}
