using Phantasma.Infrastructure.API;

namespace Phantasma.Infrastructure.Tests.API;

public class APIFailCaseAttributeTests
{
    [Fact]
    public void APIFailCaseAttribute_StoresDescriptionAndValue()
    {
        var description = "Test Description";
        var value = "Test Value";
        var apiFailCaseAttribute = new APIFailCaseAttribute(description, value);

        Assert.Equal(description, apiFailCaseAttribute.Description);
        Assert.Equal(value, apiFailCaseAttribute.Value);
    }
}
