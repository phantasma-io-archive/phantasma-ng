using Phantasma.Core.Storage.Context;
using Xunit;

namespace Phantasma.Core.Tests.Storage.Context;

public class StorageSetTests
{
    [Fact]
    public void TestCount()
    {
        // Arrange
        var storage = new MemoryStorageContext();
        var set = new StorageSet(new byte[] { 1, 2, 3 }, storage);

        // Act
        var count = set.Count();

        // Assert
        Assert.Equal(0, count);
    }
    
    [Fact]
    public void TestContains()
    {
        // Arrange
        var storage = new MemoryStorageContext();
        var set = new StorageSet(new byte[] { 1, 2, 3 }, storage);
        set.Add(1);

        // Act
        var contains = set.Contains(1);

        // Assert
        Assert.True(contains);
    }

    [Fact]
    public void TestAdd()
    {
        // Arrange
        var storage = new MemoryStorageContext();
        var set = new StorageSet(new byte[] { 1, 2, 3 }, storage);

        // Act
        set.Add(1);
        set.Add(2);
        set.Add(3);
        var count = set.Count();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void TestRemove()
    {
        // Arrange
        var storage = new MemoryStorageContext();
        var set = new StorageSet(new byte[] { 1, 2, 3 }, storage);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        // Act
        set.Remove(2);
        var count = set.Count();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact (Skip = "This test needs fixing")]
    public void TestAllValues()
    {
        // Arrange
        var storage = new MemoryStorageContext();
        var set = new StorageSet(new byte[] { 1, 2, 3 }, storage);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        // Act
        var values = set.AllValues<int>();

        // Assert
        Assert.Equal(new int[] { 1, 2, 3 }, values);
    }
}
