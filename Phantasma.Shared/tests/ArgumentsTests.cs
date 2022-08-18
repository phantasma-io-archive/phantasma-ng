using System;
using Phantasma.Shared.Utils;
using Shouldly;
using Xunit;

namespace Phantasma.Shared.Tests;

public class ArgumentsTests
{
    [Fact]
    public void GetDefaultValue_should_return_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test", "run" });

        // Act
        var result = sut.GetDefaultValue();

        // Assert
        result.ShouldBe("run");
    }

    [Fact]
    public void GetDefaultValue_should_throw_when_no_default_argument_exists()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = Should.Throw<Exception>(() => sut.GetDefaultValue());

        // Assert
        result.Message.ShouldBe("Not default argument found");
    }

    [Fact]
    public void GetString_should_return_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = sut.GetString("arg1");

        // Assert
        result.ShouldBe("test");
    }

    [Fact]
    public void GetString_should_return_default_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = sut.GetString("arg2", "notset");

        // Assert
        result.ShouldBe("notset");
    }

    [Fact]
    public void GetString_should_throw_when_required_key_is_not_found()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = Should.Throw<Exception>(() => sut.GetString("arg2", required: true));

        // Assert
        result.Message.ShouldBe("Unconfigured setting: arg2");
    }

    [Fact]
    public void GetString_should_throw_when_key_is_not_found_and_default_value_is_null()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = Should.Throw<Exception>(() => sut.GetString("arg2"));

        // Assert
        result.Message.ShouldBe("Missing non-optional argument: arg2");
    }

    [Fact]
    public void GetInt_should_return_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=1" });

        // Act
        var result = sut.GetInt("arg1");

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void GetInt_should_return_default_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = sut.GetInt("arg1");

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void GetUInt_should_return_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=1" });

        // Act
        var result = sut.GetUInt("arg1");

        // Assert
        result.ShouldBe(1u);
    }

    [Fact]
    public void GetUInt_should_return_default_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=-1" });

        // Act
        var result = sut.GetUInt("arg1");

        // Assert
        result.ShouldBe(0u);
    }

    [Fact]
    public void GetBool_should_return_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=true" });

        // Act
        var result = sut.GetBool("arg1");

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("bad")]
    public void GetBool_should_return_default_value(string value)
    {
        // Arrange
        var sut = new Arguments(new[] { $"-arg1={value}" });

        // Act
        var result = sut.GetBool("arg1", false);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetEnum_should_return_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=Val1" });

        // Act
        var result = sut.GetEnum<TestEnum>("arg1");

        // Assert
        result.ShouldBe(TestEnum.Val1);
    }

    [Fact]
    public void GetEnum_with_default_value_should_return_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=Val1" });

        // Act
        var result = sut.GetEnum("arg1", TestEnum.Val2);

        // Assert
        result.ShouldBe(TestEnum.Val1);
    }

    [Fact]
    public void GetEnum_should_return_default_value()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=Val3" });

        // Act
        var result = sut.GetEnum("arg1", TestEnum.Val2);

        // Assert
        result.ShouldBe(TestEnum.Val2);
    }

    [Fact]
    public void HasValue_should_return_true_when_key_exists()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = sut.HasValue("arg1");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasValue_should_return_false_when_key_does_not_exists()
    {
        // Arrange
        var sut = new Arguments(new[] { "-arg1=test" });

        // Act
        var result = sut.HasValue("arg2");

        // Assert
        result.ShouldBeFalse();
    }

    private enum TestEnum
    {
        Val1,
        Val2
    }
}
