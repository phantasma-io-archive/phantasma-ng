using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Oracle;
using Xunit;

namespace Phantasma.Core.Tests.Domain;

public class OracleEntryTests
{
    [Fact]
    public void TestEquality()
    {
        // Arrange
        var bytesEquals = new byte[] { 0x01, 0x02, 0x03 };
        var entry1 = new OracleEntry("https://www.example.com", bytesEquals);
        var entry2 = new OracleEntry("https://www.example.com", bytesEquals);
        var entry3 = new OracleEntry("https://www.example.org", bytesEquals);
        var entry4 = new OracleEntry("https://www.example.com", new byte[] { 0x03, 0x02, 0x01 });
        var entry5 = new OracleFeed();


        // Act and assert
        Assert.True(entry1.Equals(entry2));
        Assert.False(entry1.Equals(entry3));
        Assert.False(entry1.Equals(entry4));
        Assert.False(entry1.Equals(entry4));
        Assert.False(entry1.Equals(entry5));
    }

    [Fact]
    public void TestHashCode()
    {
        // Arrange
        var bytesEquals = new byte[] { 0x01, 0x02, 0x03 };
        var entry1 = new OracleEntry("https://www.example.com", bytesEquals);
        var entry2 = new OracleEntry("https://www.example.com", bytesEquals);
        var entry3 = new OracleEntry("https://www.example.org", bytesEquals);
        var entry4 = new OracleEntry("https://www.example.com", new byte[] { 0x03, 0x02, 0x01 });

        // Act and assert
        Assert.Equal(entry1.GetHashCode(), entry2.GetHashCode());
        Assert.NotEqual(entry1.GetHashCode(), entry3.GetHashCode());
        Assert.NotEqual(entry1.GetHashCode(), entry4.GetHashCode());
    }
}
