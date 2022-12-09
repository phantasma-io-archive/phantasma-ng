using System.Text;
using Phantasma.Core.Storage.Context;
using Xunit;

namespace Phantasma.Core.Tests.Storage.Context;

public class StorageValueTests
{
    private static readonly byte[] baseKey = Encoding.UTF8.GetBytes("test_key");
    private static readonly MemoryStorageContext context = new MemoryStorageContext();

    [Fact]
    public void TestSet()
    {
        var value = new StorageValue(baseKey, context);

        value.Set("hello");
        var str = value.Get<string>();
        Assert.Equal("hello", str);

        value.Set(123);
        var i = value.Get<int>();
        Assert.Equal(123, i);

        var nestedValue = new StorageValue(baseKey, context);
        value.Set(nestedValue);
        var nestedValue2 = value.Get<StorageValue>();
        Assert.Equal(nestedValue.BaseKey, nestedValue2.BaseKey);
        Assert.Equal(nestedValue.Context, nestedValue2.Context);
    }

    [Fact]
    public void TestHasValue()
    {
        var value = new StorageValue(baseKey, context);

        Assert.False(value.HasValue());

        value.Set("hello");
        Assert.True(value.HasValue());

        value.Clear();
        Assert.False(value.HasValue());
    }

    [Fact]
    public void TestClear()
    {
        var value = new StorageValue(baseKey, context);

        value.Set("hello");
        Assert.True(value.HasValue());

        value.Clear();
        Assert.False(value.HasValue());
    }
}
