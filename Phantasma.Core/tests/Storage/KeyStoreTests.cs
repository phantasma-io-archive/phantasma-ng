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
}
