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
    
    [Fact]
    public void ToString_ReturnsCorrectBase16Encoding()
    {
        // Arrange
        byte[] keyData = { 0x01, 0x02, 0x03, 0x04 };
        StorageKey key = new StorageKey(keyData);

        // Act
        string result = key.ToString();

        // Assert
        Assert.Equal("01020304", result);
    }

    [Fact]
    public void GetHashCode_ReturnsCorrectHashCode()
    {
        // Arrange
        byte[] keyData = { 0x01, 0x02, 0x03, 0x04 };
        StorageKey key = new StorageKey(keyData);

        // Act
        int result = key.GetHashCode();

        // Assert
        Assert.Equal(keyData.GetHashCode(), result);
    }
    
    [Fact]
    public void IsASCII_ReturnsTrueForASCIIKey()
    {
        // Arrange
        byte[] key = { 0x41, 0x42, 0x43, 0x44 }; // ASCII for "ABCD"

        // Act
        bool result = StorageKey.IsASCII(key);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsASCII_ReturnsFalseForNonASCIIKey()
    {
        // Arrange
        byte[] key = { 0x41, 0x42, 0x43, 0xC3, 0x84 }; // ASCII for "ABCÄ"

        // Act
        bool result = StorageKey.IsASCII(key);

        // Assert
        Assert.False(result);
    }

    [Fact (Skip = "Need to make method public")]
    public void DecodeKey_ReturnsCorrectKey()
    {
        // Arrange
        byte[] key = { 0x7B, 0x41, 0x42, 0x43, 0x44, 0x7D, 0x2E, 0x7B, 0x45, 0x46, 0x47, 0x48, 0x7D };
        // ASCII for "{ABCD}.{EFGH}"

        // Act
        //string result = StorageKey.DecodeKey(key);

        // Assert
        //Assert.Equal("ABCD.EFGH", result);
    }

    [Fact(Skip = "Need to make method public")]
    public void DecodeKey_ReturnsNullForInvalidKey()
    {
        // Arrange
        byte[] key = { 0x7B, 0x41, 0x42, 0x43, 0x44, 0x7E, 0x2E, 0x7B, 0x45, 0x46, 0x47, 0x48, 0x7D };
        // ASCII for "{ABCD}.{EFGH}" with invalid closing curly brace

        // Act
        //string result = StorageKey.DecodeKey(key);

        // Assert
        //Assert.Null(result);
    }

    [Fact]
    public void ToHumanValue_ReturnsCorrectValue()
    {
        // Arrange
        byte[] key = { 0x01, 0x02, 0x03, 0x04 };
        byte[] value = { 0x05, 0x06, 0x07, 0x08 };

        // Act
        string result = StorageKey.ToHumanValue(key, value);

        // Assert
        Assert.Equal("0x05060708", result);
    }
    
    
    
}
