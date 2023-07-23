using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Oracle;
using Phantasma.Core.Domain.Oracle.Enums;
using Phantasma.Core.Domain.Oracle.Structs;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class OracleFeedTests
{
    [Fact]
    public void TestSerialization()
    {
        // Arrange
        var address = PhantasmaKeys.Generate().Address;
        var feed = new OracleFeed("Test Feed", address, FeedMode.Average);
        var bytes = feed.ToByteArray();

        // Act
        var deserializedFeed = OracleFeed.Unserialize(bytes);

        // Assert
        Assert.Equal(feed, deserializedFeed);
    }
}
