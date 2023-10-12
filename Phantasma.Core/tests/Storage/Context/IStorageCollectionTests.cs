using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Storage.Context;
using Xunit;

namespace Phantasma.Core.Tests.Storage.Context;

public class IStorageCollectionTests
{
    [Fact]
    public void TestException()
    {
        var exception = new StorageException("test");
        Assert.Equal("test", exception.Message);
    }
}
