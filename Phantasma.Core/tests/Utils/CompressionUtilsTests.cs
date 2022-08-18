using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests.Utils;

public class CompressionUtilsTests
{
    [Fact]
    public void Compress_should_compress_byte_array()
    {
        // Arrange
        var bytes = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 };

        // Act
        var result = CompressionUtils.Compress(bytes);

        // Assert
        result.Length.ShouldBe(8);
        result.ShouldBeEquivalentTo(new byte[] { 99, 96, 128, 0, 70, 40, 0, 0 });
    }

    [Fact]
    public void Decompress_should_compress_byte_array()
    {
        // Arrange
        var bytes = new byte[] { 99, 96, 128, 0, 70, 40, 0, 0 };

        // Act
        var result = CompressionUtils.Decompress(bytes);

        // Assert
        result.Length.ShouldBe(16);
        result.ShouldBeEquivalentTo(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 });
    }
}
