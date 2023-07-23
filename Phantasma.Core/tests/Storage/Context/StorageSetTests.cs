using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Neo.VM.Types;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Utils;
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

    [Fact]
    public void TestAllValues()
    {
        // Arrange
        var storage = new MemoryStorageContext();
        var set = new StorageSet(new byte[] { 1, 2, 3 }, storage);
        /*var storageData = new StorageSet(new byte[]{4,5,6}, storage);*/
        var key = new byte[] { 1, 2, 3 };
        var myKey = ByteArrayUtils.ConcatBytes(key, Encoding.UTF8.GetBytes("MyKey"));
        ByteArrayUtils.ConcatBytes(key, new BigInteger(200).ToByteArray());
        //var StorageKey = new StorageKey(key + Encoding.UTF8.GetBytes("MyKey"));
        set.Context.Put(key, (int) 100);
        
        //set.Add((BigInteger) 100);
        //set.Add((BigInteger) 200);
        
        // Act
        var values = SetUtils.AllValues<int>(set);

        // Assert
        Assert.Equal(1, values.Length);
        Assert.Equal(100, values[0]);
        
    }
}
