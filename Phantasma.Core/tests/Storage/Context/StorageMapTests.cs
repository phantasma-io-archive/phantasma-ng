using System.Text;
using Phantasma.Core.Storage.Context;
using Xunit;

namespace Phantasma.Core.Tests.Storage.Context;

public class StorageMapTests
{
    [Fact]
    public void TestStorageMapByte()
    {
        var context = new MemoryStorageContext();

        var baseKey = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var map = new StorageMap(baseKey, context);
        
        Assert.Equal(baseKey, map.BaseKey);
        Assert.Equal(context, map.Context);
    }

    [Fact]
    public void TestStorageMapString()
    {
        var context = new MemoryStorageContext();
        var baseKey = "baseKey";
        var map = new StorageMap(baseKey, context);
        
        Assert.Equal(Encoding.UTF8.GetBytes(baseKey), map.BaseKey);
        Assert.Equal(context, map.Context);
    }
}
