using System;
using System.Linq;
using System.Threading;

namespace Phantasma.Core.Tests.Utils;

using Xunit;
using Phantasma.Core.Utils;

public class CacheTests
{
    /* write tests to test the Cache class */
    [Fact]
    public void CacheTest()
    {
        // create a new cache with a capacity of 10 with a duration of 1 hour
        var cache = new Cache<string>(10, TimeSpan.FromHours(1));
        
        // add a new item to the cache
        cache.Add("test");
        
        // check if the cache contains the item
        Assert.True(cache.Items.Contains("test"));
    }
    
    // Create a cache tests and add multiple items to the cache
    [Fact]
    public void CacheTestMultiple()
    {
        // create a new cache with a capacity of 10 with a duration of 1 hour
        var cache = new Cache<string>(10, TimeSpan.FromHours(1));
        
        // add a new item to the cache
        cache.Add("test");
        cache.Add("test2");
        cache.Add("test3");
        
        // check if the cache contains the item
        Assert.True(cache.Items.Contains("test"));
        Assert.True(cache.Items.Contains("test2"));
        Assert.True(cache.Items.Contains("test3"));
    }
    
    [Fact]
    public void CacheTestMultipleLowTime()
    {
        // create a new cache with a capacity of 10 with a duration of 200 Miliseconds
        var cache = new Cache<string>(10, TimeSpan.FromMilliseconds(200));
        
        // add a new item to the cache
        cache.Add("test");
        cache.Add("test2");
        cache.Add("test3");
        
        Thread.Sleep(200);
        
        cache.Add("test4");
        
        // check if the cache contains the item
        Assert.False(cache.Items.Contains("test"));
        Assert.False(cache.Items.Contains("test2"));
        Assert.False(cache.Items.Contains("test3"));
        Assert.True(cache.Items.Contains("test4"));
    }
    
    /* write tests to test the Cache class */
    [Fact]
    public void TestCacheDictionary()
    {
        // create a new cache dictionary with a capacity of 10 with a duration of 1 hour not infinite
        var cache = new CacheDictionary<string, string>(10, TimeSpan.FromHours(1), false);

        // add a new item to the cache
        cache.Add("test", "test");
        
        // check if the cache contains the item
        cache.TryGet("test", out var value);
        Assert.True(value == "test");
        
        // remove the item from the cache
        cache.Remove("test");
        
        // check if the cache contains the item
        cache.TryGet("test", out value);
        Assert.True(value == null);
    }
    
    [Fact]
    public void TestCacheDictionaryMultiple()
    {
        // create a new cache dictionary with a capacity of 10 with a duration of 1 hour not infinite
        var cache = new CacheDictionary<string, string>(10, TimeSpan.FromHours(1), false);

        // add a new item to the cache
        cache.Add("test", "test");
        cache.Add("test2", "test2");
        cache.Add("test3", "test3");
        
        // check if the cache contains the item
        cache.TryGet("test", out var value);
        Assert.True(value == "test");
        
        cache.TryGet("test2", out value);
        Assert.True(value == "test2");
        
        cache.TryGet("test3", out value);
        Assert.True(value == "test3");
        
        // remove the item from the cache
        cache.Remove("test");
        cache.Remove("test2");
        cache.Remove("test3");
        
        // check if the cache contains the item
        cache.TryGet("test", out value);
        Assert.True(value == null);
        
        cache.TryGet("test2", out value);
        Assert.True(value == null);
        
        cache.TryGet("test3", out value);
        Assert.True(value == null);
    }
}
