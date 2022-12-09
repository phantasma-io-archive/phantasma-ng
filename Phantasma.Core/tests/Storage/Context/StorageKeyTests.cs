using Phantasma.Core.Storage.Context;
using Xunit;

namespace Phantasma.Core.Tests.Storage.Context;

public class StorageKeytests
{
    [Fact]
    public void TestToString()
    {
        // Arrange
        var keyData = new byte[] { 0x01, 0x02, 0x03 };
        var storageKey = new StorageKey(keyData);

        // Act
        var result = storageKey.ToString();

        // Assert
        Assert.Equal("010203", result);
    }

    [Fact]
    public void TestGetHashCode()
    {
        // Arrange
        var keyData = new byte[] { 0x01, 0x02, 0x03 };
        var storageKey = new StorageKey(keyData);

        // Act
        var result = storageKey.GetHashCode();

        // Assert
        Assert.Equal(keyData.GetHashCode(), result);
    }

    [Fact]
    public void TestIsASCII()
    {
        // Arrange
        var key1 = new byte[] { 0x01, 0x02, 0x03 };
        var key2 = new byte[] { 0x50 };
        var key3 = new byte[] { 0x1F, 0x80 };

        // Act
        var result1 = StorageKey.IsASCII(key1);
        var result2 = StorageKey.IsASCII(key2);
        var result3 = StorageKey.IsASCII(key3);

        // Assert
        Assert.False(result1);
        Assert.True(result2);
        Assert.False(result3);
    }
}
