using Phantasma.Infrastructure.API;

namespace Phantasma.Infrastructure.Tests.API;

public class APIParameterAttributeTests
{
    [Fact]
    public void APIParameterAttribute_StoresPropertiesCorrectly()
    {
        var description = "Test Description";
        var value = "Test Value";

        var apiParameterAttribute = new APIParameterAttribute(description, value);

        Assert.Equal(description, apiParameterAttribute.Description);
        Assert.Equal(value, apiParameterAttribute.Value);
    }
}
