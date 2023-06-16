using System;

namespace Phantasma.Infrastructure.API;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = true)]
public class APIParameterAttribute : APIDescriptionAttribute
{
    public readonly string Value;

    public APIParameterAttribute(string description, string value) : base(description)
    {
        Value = value;
    }
}