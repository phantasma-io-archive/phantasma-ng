using System;
using Phantasma.Core.Domain;

namespace Phantasma.Business.Tests.CodeGen.Assembler;

using Xunit;

using Phantasma.Business.CodeGen.Assembler;

public class ArgumentUtilsTests
{
    [Fact]
    public void TestIsString()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.IsString());

        // Test string with only one quote
        var stringWithOneQuote = "\"hello";
        Assert.False(stringWithOneQuote.IsString());

        // Test string with two quotes
        var stringWithTwoQuotes = "\"hello\"";
        Assert.True(stringWithTwoQuotes.IsString());

        // Test string with three quotes
        var stringWithThreeQuotes = "\"hello\"world\"";
        Assert.True(stringWithThreeQuotes.IsString());
    }

    [Fact]
    public void TestIsRegister()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.IsRegister());

        // Test string with only one character
        var stringWithOneCharacter = "r";
        Assert.False(stringWithOneCharacter.IsRegister());

        // Test register string with prefix "r"
        var registerStringWithPrefixR = "r1";
        Assert.True(registerStringWithPrefixR.IsRegister());

        // Test register string with prefix "$"
        var registerStringWithPrefixDollar = "$r1";
        Assert.True(registerStringWithPrefixDollar.IsRegister());

        // Test string starting with "r" but not a register
        var stringStartingWithRButNotARegister = "rhello";
        Assert.True(stringStartingWithRButNotARegister.IsRegister());
    }
    
    [Fact]
    public void TestIsAlias()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.IsAlias());

        // Test string with only one character
        var stringWithOneCharacter = "$";
        Assert.False(stringWithOneCharacter.IsAlias());

        // Test alias string with prefix "$"
        var aliasStringWithPrefixDollar = "$r1";
        Assert.True(aliasStringWithPrefixDollar.IsAlias());

        // Test string starting with "$" but not an alias
        var stringStartingWithDollarButNotAnAlias = "$hello";
        Assert.True(stringStartingWithDollarButNotAnAlias.IsAlias());
    }
    
     [Fact]
    public void TestIsLabel()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.IsLabel());

        // Test string with only one character
        var stringWithOneCharacter = "@";
        Assert.False(stringWithOneCharacter.IsLabel());

        // Test label string with prefix "@"
        var labelStringWithPrefix = "@hello";
        Assert.True(labelStringWithPrefix.IsLabel());

        // Test string starting with "@" but not a label
        var stringStartingWithAtButNotALabel = "@1";
        Assert.True(stringStartingWithAtButNotALabel.IsLabel());
    }

    [Fact]
    public void TestIsNumber()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.IsNumber());

        // Test string with non-numeric characters
        var stringWithNonNumericCharacters = "hello";
        Assert.False(stringWithNonNumericCharacters.IsNumber());

        // Test string with numeric characters
        var stringWithNumericCharacters = "123";
        Assert.True(stringWithNumericCharacters.IsNumber());
    }

    [Fact]
    public void TestIsBytes()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.IsBytes());

        // Test string with fewer than 3 characters
        var stringWithFewerThan3Characters = "0x1";
        Assert.False(stringWithFewerThan3Characters.IsBytes());

        // Test string with prefix "0x" but not an even number of characters
        var stringWithPrefix0xButNotEvenNumberOfCharacters = "0x12354";
        Assert.False(stringWithPrefix0xButNotEvenNumberOfCharacters.IsBytes());

        // Test string with prefix "0x" and an even number of characters
        var stringWithPrefix0xAndEvenNumberOfCharacters = "0x1234";
        Assert.True(stringWithPrefix0xAndEvenNumberOfCharacters.IsBytes());

        // Test string starting with "0x" but not a byte array
        var stringStartingWith0xButNotAByteArray = "0xhello";
        Assert.False(stringStartingWith0xButNotAByteArray.IsBytes());
    }

    [Fact]
    public void TestIsBool()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.IsBool());

        // Test string with non-boolean value
        var stringWithNonBooleanValue = "hello";
        Assert.False(stringWithNonBooleanValue.IsBool());

        // Test string with boolean value "true"
        var stringWithBooleanValueTrue = "true";
        Assert.True(stringWithBooleanValueTrue.IsBool());

        // Test string with boolean value "false"
        var stringWithBooleanValueFalse = "false";
        Assert.True(stringWithBooleanValueFalse.IsBool());
    }
    
    [Fact]
    public void TestAsString()
    {
        // Test empty string
        var emptyString = "";
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyString.AsString());

        // Test string with only one quote
        var stringWithOneQuote = "\"hello";
        Assert.NotEqual( stringWithOneQuote, stringWithOneQuote.AsString());

        // Test string with two quotes
        var stringWithTwoQuotes = "\"hello\"";
        Assert.Equal("hello", stringWithTwoQuotes.AsString());

        // Test string with three quotes
        var stringWithThreeQuotes = "\"hello\"world\"";
        Assert.NotEqual(stringWithThreeQuotes, stringWithThreeQuotes.AsString());
    }

    [Fact]
    public void TestAsAlias()
    {
        // Test empty string
        var emptyString = "";
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyString.AsAlias());

        // Test string with only one character
        var stringWithOneCharacter = "$";
        Assert.Equal( "", stringWithOneCharacter.AsAlias());

        // Test alias string with prefix "$"
        var aliasStringWithPrefixDollar = "$r1";
        Assert.Equal("r1", aliasStringWithPrefixDollar.AsAlias());

        // Test string starting with "$" but not an alias
        var stringStartingWithDollarButNotAnAlias = "$hello";
        Assert.Equal("hello", stringStartingWithDollarButNotAnAlias.AsAlias());
    }

    [Fact]
    public void TestAsType()
    {
        // Test empty string
        var emptyString = "";
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyString.AsType());

        // Test string with only one character
        var stringWithOneCharacter = "#";
        Assert.Throws<Exception>(() => stringWithOneCharacter.AsType());

        // Test type string with prefix "#"
        var typeStringWithPrefix = "#number";
        Assert.Equal((byte)VMType.Number, typeStringWithPrefix.AsType());

        // Test string starting with "#" but not a type
        var stringStartingWithHashButNotAType = "#hello";
        Assert.Throws<Exception>(() => stringStartingWithHashButNotAType.AsType());
    }

    [Fact]
    public void TestAsLabel()
    {
        // Test empty string
        var emptyString = "";
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyString.AsLabel());

        // Test string with only one character
        var stringWithOneCharacter = "@";
        Assert.Equal( "", stringWithOneCharacter.AsLabel());

        // Test label string with prefix "@"
        var labelStringWithPrefix = "@hello";
        Assert.Equal("hello", labelStringWithPrefix.AsLabel());

        // Test string starting with "@" but not a label
        var stringStartingWithAtButNotALabel = "@1";
        Assert.Equal("1",stringStartingWithAtButNotALabel.AsLabel());
    }
    
    [Fact]
    public void TestAsBool()
    {
        // Test empty string
        var emptyString = "";
        Assert.False(emptyString.AsBool());

        // Test string with non-boolean value
        var stringWithNonBooleanValue = "hello";
        Assert.False(stringWithNonBooleanValue.AsBool());

        // Test string with boolean value "true"
        var stringWithBooleanValueTrue = "true";
        Assert.True(stringWithBooleanValueTrue.AsBool());

        // Test string with boolean value "false"
        var stringWithBooleanValueFalse = "false";
        Assert.False(stringWithBooleanValueFalse.AsBool());
    }

}
