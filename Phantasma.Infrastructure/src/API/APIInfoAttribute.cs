using System;

namespace Phantasma.Infrastructure.API;

public class APIInfoAttribute : APIDescriptionAttribute
{
    public readonly Type ReturnType;
    public readonly bool Paginated;
    public readonly int CacheDuration;
    public readonly string CacheTag;
    public readonly bool InternalEndpoint;

    public APIInfoAttribute(Type returnType, string description, bool paginated = false, int cacheDuration = 0, bool internalEndpoint = false, string cacheTag = null) : base(description)
    {
        ReturnType = returnType;
        Paginated = paginated;
        CacheDuration = cacheDuration;
        InternalEndpoint = internalEndpoint;
        CacheTag = cacheTag;
    }
}