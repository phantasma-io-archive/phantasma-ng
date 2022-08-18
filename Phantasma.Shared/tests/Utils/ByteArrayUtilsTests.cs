using Phantasma.Shared.Utils;
using Shouldly;
using Xunit;

namespace Phantasma.Shared.Tests.Utils;

public class ByteArrayUtilsTests
{
    [Fact]
    public void ConcatBytes_should_return_value_of_combined_byte_arrays()
    {
        // Arrange
        var array1 = new byte[] { 0, 0, 0 };
        var array2 = new byte[] { 1, 1, 1 };

        // Act
        var result = ByteArrayUtils.ConcatBytes(array1, array2);

        // Assert
        result.ShouldNotBeEmpty();
        result.Length.ShouldBe(6);
        result.ShouldBeEquivalentTo(new byte[] { 0, 0, 0, 1, 1, 1 });
    }

    [Fact]
    public void ConcatBytes_should_return_value_of_byte_combined_with_byte_arrays()
    {
        // Arrange
        var value = (byte)1;
        var array = new byte[] { 1, 1, 1 };

        // Act
        var result = ByteArrayUtils.ConcatBytes(value, array);

        // Assert
        result.ShouldNotBeEmpty();
        result.Length.ShouldBe(4);
        result.ShouldBeEquivalentTo(new byte[] { 1, 1, 1, 1 });
    }

    [Fact]
    public void DupBytes_should_return_null_when_input_byte_array_is_null()
    {
        // Arrange
        var array = (byte[])null;

        // Act
        var result = ByteArrayUtils.DupBytes(array);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void DupBytes_should_return_copy_of_input_byte_array_when_not_null()
    {
        // Arrange
        var array = new byte[] { 1, 1, 1 };

        // Act
        var result = ByteArrayUtils.DupBytes(array);

        // Assert
        result.ShouldNotBeEmpty();
        result.Length.ShouldBe(3);
        result.ShouldBeEquivalentTo(array);
    }

    [Fact]
    public void RangeBytes_should_return_value_of_byte_array_within_range()
    {
        // Arrange
        var array = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = array.RangeBytes(1, 3);

        // Assert
        result.ShouldNotBeEmpty();
        result.Length.ShouldBe(3);
        result.ShouldBeEquivalentTo(new byte[] { 2, 3, 4 });
    }

    [Fact]
    public void ReverseBytes_should_return_value_of_byte_array_in_reverse_order()
    {
        // Arrange
        var array = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = ByteArrayUtils.ReverseBytes(array);

        // Assert
        result.ShouldNotBeEmpty();
        result.Length.ShouldBe(5);
        result.ShouldBeEquivalentTo(new byte[] { 5, 4, 3, 2, 1 });
    }

    [Fact]
    public void SearchBytes_should_return_start_index_when_needle_is_found()
    {
        // Arrange
        var array = new byte[] { 1, 2, 3, 4, 5 };
        var needle = new byte[] { 2, 3, 4 };

        // Act
        var result = array.SearchBytes(needle);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void SearchBytes_should_return_negative_index_when_needle_is_not_found()
    {
        // Arrange
        var array = new byte[] { 1, 2, 3, 4, 5 };
        var needle = new byte[] { 6, 7 };

        // Act
        var result = array.SearchBytes(needle);

        // Assert
        result.ShouldBe(-1);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(new byte[] { 0, 1, 0 }, new byte[] { 0, 1, 0 })]
    public void CompareBytes_should_return_true_for_matching_byte_arrays(byte[] array1, byte[] array2)
    {
        // Act
        var result = array1.CompareBytes(array2);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(new byte[] { 0, 1, 0 }, new byte[] { 0, 1, 1 })]
    [InlineData(new byte[] { 0, 1, 0 }, new byte[] { 0, 1, 0, 0 })]
    public void CompareBytes_should_return_false_for_non_matching_byte_arrays(byte[] array1, byte[] array2)
    {
        // Act
        var result = array1.CompareBytes(array2);

        // Assert
        result.ShouldBeFalse();
    }
}
