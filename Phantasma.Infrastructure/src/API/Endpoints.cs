using System;

namespace Phantasma.Infrastructure
{
    public class APIException : Exception
    {
        public APIException(string msg) : base(msg)
        {
        }

        public APIException(string msg, Exception innerException) : base(msg, innerException)
        {
        }
    }

    public class APIDescriptionAttribute : Attribute
    {
        public readonly string Description;

        public APIDescriptionAttribute(string description)
        {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class APIFailCaseAttribute : APIDescriptionAttribute
    {
        public readonly string Value;

        public APIFailCaseAttribute(string description, string value) : base(description)
        {
            Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = true)]
    public class APIParameterAttribute : APIDescriptionAttribute
    {
        public readonly string Value;

        public APIParameterAttribute(string description, string value) : base(description)
        {
            Value = value;
        }
    }

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
}
