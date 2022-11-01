using Phantasma.Core.Utils;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Utils;

public class ByteArrayComparerTests
{
    [Fact]
    public void Equals_should_return_true_when_byte_arrays_match()
    {
        // Arrange
        var sut = new ByteArrayComparer();
        var array1 = new byte[] { 0, 1, 2 };
        var array2 = new byte[] { 0, 1, 2 };

        // Act
        var result = sut.Equals(array1, array2);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Equals_should_return_false_when_byte_arrays_do_not_match()
    {
        // Arrange
        var sut = new ByteArrayComparer();
        var array1 = new byte[] { 0, 1, 2 };
        var array2 = new byte[] { 0, 1, 3 };

        // Act
        var result = sut.Equals(array1, array2);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_should_return_calculated_hash_code_for_byte_array()
    {
        // Arrange
        var sut = new ByteArrayComparer();
        var array1 = new byte[] { 0, 1, 2 };

        // Act
        var result = sut.GetHashCode(array1);

        // Assert
        result.ShouldBe(59334);
    }
}
