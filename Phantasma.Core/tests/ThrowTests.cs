using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests;

public class ThrowTests
{
    [Fact]
    public void IfNull_should_throw_ArgumentNullException_when_null()
    {
        // Act
        var result = Should.Throw<ArgumentNullException>(() => Throw.IfNull(null, "testArgument"));

        // Assert
        result.ParamName.ShouldBe("testArgument");
    }

    [Fact]
    public void IfNull_should_not_throw_ArgumentNullException_when_not_null()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfNull("test", "testArgument"));
    }

    [Fact]
    public void IfNullGeneric_should_throw_ArgumentException_when_null()
    {
        // Act
        var result = Should.Throw<ArgumentException>(() => Throw.IfNull((TestStruct?)null, "testArgument"));

        // Assert
        result.ParamName.ShouldBe("testArgument");
        result.Message.ShouldStartWith("Cannot be an invalid value");
    }

    [Fact]
    public void IfNullGeneric_should_not_throw_ArgumentException_when_not_null()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfNull(new TestStruct { Name = "Test" }, "testArgument"));
    }

    [Fact]
    public void IfNullOrEmpty_should_throw_ArgumentException_when_empty()
    {
        // Act
        var result = Should.Throw<ArgumentException>(() => Throw.IfNullOrEmpty("", "testArgument"));

        // Assert
        result.ParamName.ShouldBe("testArgument");
        result.Message.ShouldStartWith("Cannot be an empty string");
    }

    [Fact]
    public void IfNullOrEmpty_should_not_throw_ArgumentNullException_when_not_null()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfNullOrEmpty("test", "testArgument"));
    }

    [Fact]
    public void IfNullOrEmptyCollection_should_throw_ArgumentException_when_empty()
    {
        // Act
        var result = Should.Throw<ArgumentException>(() => Throw.IfNullOrEmpty(new List<string>(), "testArgument"));

        // Assert
        result.ParamName.ShouldBe("testArgument");
        result.Message.ShouldStartWith("Cannot be an empty collection");
    }

    [Fact]
    public void IfNullOrEmptyCollection_should_not_throw_ArgumentNullException_when_not_null()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfNullOrEmpty(new List<string> { "test" }, "testArgument"));
    }

    [Fact]
    public void IfHasNullGeneric_should_throw_ArgumentException_when_collection_contains_null()
    {
        // Act
        var result = Should.Throw<ArgumentException>(() => Throw.IfHasNull(new List<string> { null }, "testArgument"));

        // Assert
        result.ParamName.ShouldBe("testArgument");
        result.Message.ShouldStartWith("Cannot contain a null item in the collection");
    }

    [Fact]
    public void IfHasNullGeneric_should_not_throw_ArgumentNullException_when_collection_contains_no_null()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfHasNull(new List<string> { "test" }, "testArgument"));
    }

    [Fact]
    public void IfEmptyGeneric_should_throw_ArgumentException_when_collection_is_empty()
    {
        // Act
        var result = Should.Throw<ArgumentException>(() => Throw.IfEmpty(new List<string>(), "testArgument"));

        // Assert
        result.ParamName.ShouldBe("testArgument");
        result.Message.ShouldStartWith("Collection must contain at least one item");
    }

    [Fact]
    public void IfHasNullGeneric_should_not_throw_ArgumentNullException_when_collection_is_not_empty()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfEmpty(new List<string> { "test" }, "testArgument"));
    }

    [Fact]
    public void IfEmpty_should_throw_ArgumentException_when_empty()
    {
        // Act
        var result = Should.Throw<ArgumentException>(() => Throw.IfEmpty("", "testArgument"));

        // Assert
        result.ParamName.ShouldBe("testArgument");
        result.Message.ShouldStartWith("Cannot be an empty string");
    }

    [Fact]
    public void IfHasNull_should_not_throw_ArgumentNullException_when_not_empty()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfEmpty("test", "testArgument"));
    }

    [Fact]
    public void If_should_throw_Exception_when_true()
    {
        // Act
        var result = Should.Throw<Exception>(() => Throw.If(true, "testConstraint"));

        // Assert
        result.Message.ShouldStartWith("Constraint failed");
    }

    [Fact]
    public void If_should_not_throw_Exception_when_false()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.If(false, "testConstraint"));
    }

    [Fact]
    public void IfFunc_should_throw_Exception_when_true()
    {
        // Act
        var result = Should.Throw<Exception>(() => Throw.If(() => true, "testConstraint"));

        // Assert
        result.Message.ShouldStartWith("Constraint failed");
    }

    [Fact]
    public void IfFunc_should_not_throw_Exception_when_false()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.If(() => false, "testConstraint"));
    }

    [Fact]
    public void IfNot_should_throw_Exception_when_false()
    {
        // Act
        var result = Should.Throw<Exception>(() => Throw.IfNot(false, "testConstraint"));

        // Assert
        result.Message.ShouldStartWith("Constraint failed");
    }

    [Fact]
    public void IfNot_should_not_throw_Exception_when_true()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfNot(true, "testConstraint"));
    }

    [Fact]
    public void IfNotFunc_should_throw_Exception_when_false()
    {
        // Act
        var result = Should.Throw<Exception>(() => Throw.IfNot(() => false, "testConstraint"));

        // Assert
        result.Message.ShouldStartWith("Constraint failed");
    }

    [Fact]
    public void IfNotFunc_should_not_throw_Exception_when_true()
    {
        // Act and Assert
        Should.NotThrow(() => Throw.IfNot(() => true, "testConstraint"));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(10, 1, 9)]
    [InlineData(-1, 0, 9)]
    public void IfOutOfRange_should_throw_ArgumentOutOfRangeException_when_out_of_range(int argumentValue,
        int startRange, int endRange)
    {
        // Act
        var result = Should.Throw<ArgumentOutOfRangeException>(() =>
            Throw.IfOutOfRange(argumentValue, startRange, endRange, "testArgument"));

        // Assert
        result.Message.ShouldStartWith("Cannot be outside the range");
    }

    [Fact]
    public void IfOutOfRange_EnumOutOfRangeValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var argumentValue = (TestEnum)10;

        // Act
        var result = Should.Throw<ArgumentOutOfRangeException>(() => Throw.IfOutOfRange(argumentValue, "testArgument"));

        // Assert
        result.Message.ShouldStartWith("Cannot be a value outside the specified enum range");
    }

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(5, 0, 10)]
    [InlineData(-1, -2, 9)]
    public void IfInRange_should_throw_ArgumentOutOfRangeException_when_in_range(int argumentValue, int startRange,
        int endRange)
    {
        // Act
        var result = Should.Throw<ArgumentOutOfRangeException>(() =>
            Throw.IfInRange(argumentValue, startRange, endRange, "testArgument"));

        // Assert
        result.Message.ShouldStartWith("Cannot be inside the range");
    }

    [ExcludeFromCodeCoverage]
    private struct TestStruct
    {
        public string Name { get; set; }
    }

    private enum TestEnum
    {
        Test
    }
}
