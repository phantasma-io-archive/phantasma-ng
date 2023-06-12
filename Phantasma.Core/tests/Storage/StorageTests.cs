using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Core.Storage;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Xunit;

namespace Phantasma.Core.Tests.Storage;

[Collection("Storage")]
public class StorageTests
{
   [Fact] 
    public void TestStorageList()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));
        
        var list = new StorageList("test", context);
        Assert.True(list.Count() == 0);

        list.Add("hello");
        list.Add("world");
        Assert.True(list.Count() == 2);

        list.RemoveAt(0);
        Assert.True(list.Count() == 1);

        var temp = list.Get<string>(0);
        Assert.True(temp == "world");

        list.Replace<string>(0, "hello");

        temp = list.Get<string>(0);
        Assert.True(temp == "hello");
    }

    [Fact] 
    public void TestStorageMap()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));


        var map = new StorageMap("test".AsByteArray(), context);
        Assert.True(map.Count() == 0);

        map.Set(1, "hello");
        map.Set(3, "world");
        Assert.True(map.Count() == 2);

        Assert.False(map.ContainsKey(0));
        Assert.True(map.ContainsKey(1));
        Assert.False(map.ContainsKey(2));
        Assert.True(map.ContainsKey(3));

        map.Remove(2);
        Assert.True(map.Count() == 2);

        map.Remove(1);
        Assert.True(map.Count() == 1);
    }
    
    [Fact] 
    public void TestStorageMapClear()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));

        var map = new StorageMap("test".AsByteArray(), context);
        Assert.True(map.Count() == 0);

        map.Set(1, "hello");
        map.Set(3, "world");
        Assert.True(map.Count() == 2);

        Assert.False(map.ContainsKey(0));
        Assert.True(map.ContainsKey(1));
        Assert.False(map.ContainsKey(2));
        Assert.True(map.ContainsKey(3));

        map.Clear();
        Assert.True(map.Count() == 0);
    }

    [Fact] 
    public void TestStorageMapClearEmpty()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));


        var map = new StorageMap("test".AsByteArray(), context);
        Assert.True(map.Count() == 0);
        map.Clear();
        Assert.True(map.Count() == 0);
    }

    [Fact] 
    public void TestStorageMapBigInt()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));


        var map = new StorageMap("test".AsByteArray(), context);
        Assert.True(map.Count() == 0);

        var big = BigInteger.Parse("1111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111");
        map.Set<BigInteger, BigInteger>(big, big);
        Assert.True(map.Count() == 1);

        Assert.True(map.ContainsKey<BigInteger>(big));

        map.Remove<BigInteger>(big);
        Assert.True(map.Count() == 0);
    }

    [Fact] 
    public void TestStorageMapAllValues()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));


        var map = new StorageMap("test".AsByteArray(), context);
        Assert.True(map.Count() == 0);

        var big1 = BigInteger.Parse("1111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111");
        var big2 = BigInteger.Parse("2222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222222");
        var big3 = BigInteger.Parse("3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333");

        map.Set<BigInteger, BigInteger>(big1, big1);
        map.Set<BigInteger, BigInteger>(big2, big2);
        map.Set<BigInteger, BigInteger>(big3, big3);
        Assert.True(map.Count() == 3);

        Assert.True(map.ContainsKey<BigInteger>(big1));
        Assert.True(map.ContainsKey<BigInteger>(big2));
        Assert.True(map.ContainsKey<BigInteger>(big3));

        var all = map.AllValues<BigInteger>();
        foreach(var x in all)
        {
            System.Console.WriteLine(x);
        }

        System.Console.WriteLine("COUNT: " + all.Count());
        Assert.True(all.Count() == 3);
    }

    [Fact] 
    public void TestStorageListWithNestedMap()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));


        var map = new StorageMap("map".AsByteArray(), context);
        Assert.True(map.Count() == 0);

        map.Set(1, "hello");
        map.Set(3, "world");
        var count = map.Count();
        Assert.True(count == 2);

        var list = new StorageList("list".AsByteArray(), context);
        Assert.True(list.Count() == 0);

        list.Add(map);

        count = list.Count();
        Assert.True(count == 1);

        var another = list.Get<StorageMap>(0);
        count = another.Count();
        Assert.True(count == 2);
    }

    [Fact] 
    public void TestStorageMapWithNestedList()
    {
        var context = new StorageChangeSetContext(new KeyStoreStorage(new MemoryStore()));


        var list = new StorageList("list".AsByteArray(), context);
        Assert.True(list.Count() == 0);

        var map = new StorageMap("map".AsByteArray(), context);
        Assert.True(map.Count() == 0);
        int key = 123;
        map.Set(key, list);

        list.Add("hello");
        list.Add("world");
        var count = list.Count();
        Assert.True(count == 2);

        count = map.Count();
        Assert.True(count == 1);

        int otherKey = 21;
        var other = map.Get<int, StorageList>(otherKey);
        Assert.True(other.Count() == 0);

        var another = map.Get<int, StorageList>(key);
        count = another.Count();
        Assert.True(count == 2);

        // note: here we remove from one list and count the other, should be same since both are references to same storage list
        another.RemoveAt(0);
        count = list.Count();
        Assert.True(count == 1);
    }
}