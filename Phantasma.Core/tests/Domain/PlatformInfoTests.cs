namespace Phantasma.Core.Tests.Domain;
using Phantasma.Core.Domain;
using Xunit;
public class PlatformInfoTests
{
    // Write some tests with xUnit to test the PlatformInfo class
    [Fact(Skip = "Not Implemented")]
    public void GetPlatformInfo()
    {
        // Arrange
        var platformInfo = new PlatformInfo();
        // Act
        var result = platformInfo.Serialize();
        // Assert
        Assert.NotNull(result);
    }
}
