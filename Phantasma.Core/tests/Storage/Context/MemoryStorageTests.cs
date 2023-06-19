using System.Collections.Generic;
using System.Text;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Xunit;

namespace Phantasma.Core.Tests.Storage.Context;

public class MemoryStorageTests
{
    [Fact]
    public void TestClear()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key = new StorageKey(new byte[] { 1, 2, 3 });
        var value = new byte[] { 4, 5, 6 };
        storageContext.Put(key, value);

        // Act
        storageContext.Clear();

        // Assert
        Assert.False(storageContext.Has(key));
        Assert.Equal(new byte[0], storageContext.Get(key));
    }

    [Fact]
    public void TestHas()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key = new StorageKey(new byte[] { 1, 2, 3 });
        var value = new byte[] { 4, 5, 6 };
        storageContext.Put(key, value);

        // Act and Assert
        Assert.True(storageContext.Has(key));
    }

    [Fact]
    public void TestGet()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        storageContext = new MemoryStorageContext();
        var key = new StorageKey(new byte[] { 1, 2, 3 });
        var value = new byte[] { 4, 5, 6 };
        storageContext.Put(key, value);

        // Act and Assert
        Assert.Equal(value, storageContext.Get(key));
    }

    [Fact]
    public void TestPut()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key = new StorageKey(new byte[] { 1, 2, 3 });
        var value = new byte[] { 4, 5, 6 };

        // Act
        storageContext.Put(key, value);

        // Assert
        Assert.True(storageContext.Has(key));
        Assert.Equal(value, storageContext.Get(key));
    }
    
    [Fact]
    public void TestPutNull()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key = new StorageKey(new byte[] { 1, 2, 3 });
        var value = null as byte[];

        // Act
        storageContext.Put(key, value);

        // Assert
        Assert.Equal(new byte[0], storageContext.Get(key));
    }

    [Fact]
    public void TestDelete()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key = new StorageKey(new byte[] { 1, 2, 3 });
        var value = new byte[] { 4, 5, 6 };
        storageContext.Put(key, value);

        // Act
        storageContext.Delete(key);

        // Assert
        Assert.False(storageContext.Has(key));
        Assert.Equal(new byte[0], storageContext.Get(key));
    }
    
    [Fact]
    public void TestVisit()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key1 = new StorageKey(new byte[] { 1, 2, 3 });
        var value1 = new byte[] { 4, 5, 6 };
        var key2 = new StorageKey(new byte[] { 7, 8, 9 });
        var value2 = new byte[] { 10, 11, 12 };
        storageContext.Put(key1, value1);
        storageContext.Put(key2, value2);
        var visitedKeys = new List<StorageKey>();
        var visitedValues = new List<byte[]>();
        // Act
        storageContext.Visit((key, value) =>
        {
            visitedKeys.Add(new StorageKey(key));
            visitedValues.Add(value);
        }, 2, Encoding.UTF8.GetBytes(""));

        // Assert
        Assert.Equal(new[] { key1, key2 }, visitedKeys);
        Assert.Equal(new[] { value1, value2 }, visitedValues);
    }
    
    [Fact]
    public void TestVisit_WithSearchCount()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key1 = new StorageKey(new byte[] { 1, 2, 3 });
        var value1 = new byte[] { 4, 5, 6 };
        var key2 = new StorageKey(new byte[] { 7, 8, 9 });
        var value2 = new byte[] { 10, 11, 12 };
        storageContext.Put(key1, value1);
        storageContext.Put(key2, value2);
        var visitedKeys = new List<StorageKey>();
        var visitedValues = new List<byte[]>();

        // Act
        storageContext.Visit((key, value) =>
        {
            visitedKeys.Add(new StorageKey(key));
            visitedValues.Add(value);
        }, 0, Encoding.UTF8.GetBytes(""));

        // Assert
        Assert.Single(visitedKeys);
        Assert.Single(visitedValues);
    }

    [Fact]
    public void TestVisit_WithPrefix()
    {
        // Arrange
        MemoryStorageContext storageContext = new MemoryStorageContext();
        var key1 = new StorageKey(new byte[] { 1, 2, 3 });
        var value1 = new byte[] { 4, 5, 6 };
        var key2 = new StorageKey(new byte[] { 1, 2, 4 });
        var value2 = new byte[] { 10, 11, 12 };
        storageContext.Put(key1, value1);
        storageContext.Put(key2, value2);
        var visitedKeys = new List<StorageKey>();
        var visitedValues = new List<byte[]>();

        // Act
        storageContext.Visit((key, value) =>
        {
            visitedKeys.Add(new StorageKey(key));
            visitedValues.Add(value);
        }, 2, prefix: new byte[] { 1, 2 });

        // Assert
        Assert.Equal(new[] { key1, key2 }, visitedKeys);
        Assert.Equal(new[] { value1, value2 }, visitedValues);
    }
}
