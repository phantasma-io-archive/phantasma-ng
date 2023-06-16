using Phantasma.Infrastructure.API;

namespace Phantasma.Infrastructure.Tests.API;

public class APIInfoAttributeTests
{
    [Fact]
    public void APIInfoAttribute_StoresPropertiesCorrectly()
    {
        var description = "Test Description";
        var returnType = typeof(string);
        var paginated = true;
        var cacheDuration = 60;
        var internalEndpoint = false;
        var cacheTag = "Test Cache Tag";

        var apiInfoAttribute = new APIInfoAttribute(returnType, description, paginated, cacheDuration, internalEndpoint, cacheTag);

        Assert.Equal(description, apiInfoAttribute.Description);
        Assert.Equal(returnType, apiInfoAttribute.ReturnType);
        Assert.Equal(paginated, apiInfoAttribute.Paginated);
        Assert.Equal(cacheDuration, apiInfoAttribute.CacheDuration);
        Assert.Equal(internalEndpoint, apiInfoAttribute.InternalEndpoint);
        Assert.Equal(cacheTag, apiInfoAttribute.CacheTag);
    }
}
