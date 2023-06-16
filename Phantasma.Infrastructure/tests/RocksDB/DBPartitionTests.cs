using Phantasma.Infrastructure.RocksDB;

namespace Phantasma.Infrastructure.Tests.RocksDB;

public class DBPartitionTests
{
    [Fact]
    public void DBPartitionTest_ContainsKey_SetValue_GetValue_Remove()
    {
        // Arrange
        var fileName = Path.GetTempFileName();
        var partition = new DBPartition(fileName);
        var key = new byte[] { 1, 2, 3 };
        var value = new byte[] { 4, 5, 6 };

        // Act & Assert for ContainsKey, SetValue and GetValue
        Assert.False(partition.ContainsKey(key));
        partition.SetValue(key, value);
        Assert.True(partition.ContainsKey(key));
        Assert.Equal(value, partition.GetValue(key));

        // Act & Assert for Remove
        partition.Remove(key);
        Assert.False(partition.ContainsKey(key));
    }
}
