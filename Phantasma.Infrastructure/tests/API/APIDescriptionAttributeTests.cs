using Phantasma.Infrastructure.API;

namespace Phantasma.Infrastructure.Tests.API;

public class APIDescriptionAttributeTests
{
    [Fact]
    public void APIDescriptionAttribute_StoresDescription()
    {
        var description = "Test Description";
        var apiDescriptionAttribute = new APIDescriptionAttribute(description);

        Assert.Equal(description, apiDescriptionAttribute.Description);
    }
}
