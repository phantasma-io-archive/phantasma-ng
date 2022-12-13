using System;
using System.Collections.Generic;
using System.Text;

namespace Phantasma.Core.Tests.Storage;

using Xunit;
using Phantasma.Core.Storage;

[Collection("Storage")]
public class KeyStoreTests
{
    [Fact]
    public void TestMemoryStore()
    {
        // create a new MemoryStore instance
        MemoryStore store = new MemoryStore();
        
        // write a key/value pair
        byte[] key = new byte[] { 0x01, 0x02, 0x03 };
        byte[] value = new byte[] { 0x04, 0x05, 0x06 };
        store.SetValue(key, value);
        Assert.True(store.ContainsKey(key));
        
        // read the key/value pair
        byte[] result = store.GetValue(key);
        Assert.Equal(value, result);
        
        // delete the key/value pair
        store.Remove(key);
        Assert.False(store.ContainsKey(key));
        
        // read the key/value pair again
        result = store.GetValue(key);
        Assert.Null(result);
    }

    [Fact]
    public void TestBasicDiskStore()
    {
        // create a new DiskStore instance
        BasicDiskStore store = new BasicDiskStore("test.db");
        
        // write a key/value pair
        byte[] key = new byte[] { 0x01, 0x02, 0x03 };
        byte[] value = new byte[] { 0x04, 0x05, 0x06 };
        store.SetValue(key, value);
        Assert.True(store.ContainsKey(key));
        
        // Count
        Assert.Equal((uint)1, store.Count);
        
        // read the key/value pair
        byte[] result = store.GetValue(key);
        Assert.Equal(value, result);
        
        // delete the key/value pair
        store.Remove(key);
        Assert.False(store.ContainsKey(key));
        
        // read the key/value pair again
        result = store.GetValue(key);
        Assert.Null(result);
        
        // visit the store
        store.Visit((k, v) => {
            Assert.Equal(key, k);
            Assert.Equal(value, v);
        }, 1, key);
        
        // close the store
        store.Flush();
    }

    [Fact]
    public void TestKeyValueStore()
    {
        // create a new memory store
        MemoryStore memoryStore = new MemoryStore();
        // create a new KeyValueStore instance
        KeyValueStore<string, string> store = new KeyValueStore<string, string>(memoryStore);
        
        // write a key/value pair
        store.Set("key", "value");
        
        // read the key/value pair
        string result = store.Get("key");
        Assert.Equal("value", result);
        
        
        // visit the store
        store.Visit((k, v) =>
        {
            Assert.Equal("key", k);
            Assert.Equal("value", v);
        }, 1, "key".AsByteArray());

        // delete the key/value pair
        store.Remove("key");
        
        // read the key/value pair again
        store.TryGet("key", out result);
        Assert.Null(result);
    }
    
    [Fact]
    public void TestGetValue()
    {
        // Arrange
        MemoryStore memoryStore = new MemoryStore();
        KeyValueStore<int, string> store = new KeyValueStore<int, string>(memoryStore);
        store.Set(1, "value1");
        
        // Act
        var result = store.Get(1);

        // Assert
        Assert.Equal("value1", result);
    }

    [Fact]
    public void TestSetValue()
    {
        // Arrange
        MemoryStore memoryStore = new MemoryStore();
        KeyValueStore<int, string> store = new KeyValueStore<int, string>(memoryStore);
        
        // Act
        store.Set(1, "new value");
        var result = store[1];

        // Assert
        Assert.Equal("new value", result);
    }

    [Fact]
    public void TestTryGetValue()
    {
        // Arrange
        MemoryStore memoryStore = new MemoryStore();
        KeyValueStore<int, string> store = new KeyValueStore<int, string>(memoryStore);
        
        // Act
        store.Set(1, "value1");
        var result = store.TryGet(1, out var value);

        // Assert
        Assert.True(result);
        Assert.Equal("value1", value);
    }

    [Fact]
    public void TestContainsKey()
    {
        // Arrange
        MemoryStore memoryStore = new MemoryStore();
        KeyValueStore<int, string> store = new KeyValueStore<int, string>(memoryStore);
        
        // Act
        store.Set(1, "value1");
        var result = store.ContainsKey(1);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TestRemoveKey()
    {
        // Arrange
        MemoryStore memoryStore = new MemoryStore();
        KeyValueStore<int, string> store = new KeyValueStore<int, string>(memoryStore);
        
        // Act
        store.Set(1, "value");
        store.Remove(1);

        // Assert
        Assert.False(store.ContainsKey(1));
    }

    [Fact]
    public void TestVisit()
    {
        // Arrange
        MemoryStore memoryStore = new MemoryStore();
        KeyValueStore<int, string> store = new KeyValueStore<int, string>(memoryStore);
        
        // Act 
        store.Set(1, "value1");
        store.Set(2, "value2");
        store.Set(3, "value3");
        
        var keys = new List<int>();
        var values = new List<string>();
        store.Visit((key, value) =>
        {
            keys.Add(key);
            values.Add(value);
        }, 3, Encoding.UTF8.GetBytes(""));

        // Assert
        Assert.Equal(new List<int> { 1, 2, 3 }, keys);
        Assert.Equal(new List<string> { "value1", "value2", "value3" }, values);
    }
}
