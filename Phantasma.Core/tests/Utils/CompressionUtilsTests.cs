using System.Buffers;
using System.Text;
using K4os.Compression.LZ4;
using Phantasma.Core.Utils;
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
    
    [Fact]
    public void CompressLz4()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("Lorem ipsum dolor sit amet");
        int expectedCompressedLength = sizeof(uint) + LZ4Codec.Encode(data, MemoryPool<byte>.Shared.Rent(LZ4Codec.MaximumOutputSize(data.Length)).Memory.Span);

        // Act
        byte[] compressedData = data.CompressLz4();

        // Assert
        Assert.NotNull(compressedData);
        Assert.Equal(expectedCompressedLength, compressedData.Length);
    }
    
    [Fact]
    public void DecompressLz4()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("Lorem ipsum dolor sit amet");
        int maxOutput = 100;
        byte[] compressedData = data.CompressLz4();

        // Act
        byte[] decompressedData = compressedData.DecompressLz4(maxOutput);

        // Assert
        Assert.NotNull(decompressedData);
        Assert.Equal(data, decompressedData);
    }

    [Fact]
    public void DecompressLz4Throws()
    {
        
    }
}
