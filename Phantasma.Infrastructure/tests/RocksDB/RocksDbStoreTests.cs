using Phantasma.Infrastructure.RocksDB;

namespace Phantasma.Infrastructure.Tests.RocksDB;

public class RocksDbStoreTests
{
    [Fact(Skip = "TODO fix this test")]
    public void RocksDbStoreTest_InstanceCreation_Shutdown()
    {
        // Arrange
        var fileName1 = Path.GetTempFileName();
        var fileName2 = Path.GetTempFileName();

        // Act - Create instances
        var instance1 = RocksDbStore.Instance(fileName1);
        var instance2 = RocksDbStore.Instance(fileName2);
        var repeatedInstance1 = RocksDbStore.Instance(fileName1);

        // Assert - Check if instances are created and same filename points to same instance
        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.NotNull(repeatedInstance1);
        Assert.NotSame(instance1, instance2);
        Assert.Same(instance1, repeatedInstance1);

        // Shutdown and validate
        RocksDbStore.GetInstance(fileName1).Close();
        RocksDbStore.GetInstance(fileName2).Close();
        Assert.Equal(0, RocksDbStore.Count);
    }
}
